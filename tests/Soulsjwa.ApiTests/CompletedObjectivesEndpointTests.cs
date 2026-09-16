using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The HTTP contract of the objective-write endpoints only: routes exist, auth
/// and the competitor check are wired in, the scores payload has the field
/// names the frontend reads, and the edit timestamp binds from every form a
/// client may send. The rules themselves — precondition gates, duplicate
/// handling, the complete-versus-fail race, what instant an offset timestamp
/// resolves to — live in
/// <c>Soulsjwa.IntegrationTests.ObjectiveWriteTests</c>.
/// </summary>
public class CompletedObjectivesEndpointTests : ApiTestBase
{
    private const int SeededGameId = 1;

    [Fact]
    public async Task CompleteObjective_NotCompetitor_Returns403()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, strangerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "stranger");

        var (ev, eventGameId, objectiveId) = await SeedEventWithObjectiveAsync(owner.Id, ownerKey);

        var strangerClient = TestAuth.CreateAuthenticatedClient(Factory, strangerKey);
        var response = await strangerClient.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompleteObjective_Unauthenticated_Returns401()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (ev, eventGameId, objectiveId) = await SeedEventWithObjectiveAsync(owner.Id, ownerKey);

        var response = await Client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteThenUncompleteObjective_AsCompetitor_Returns201Then204()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eventGameId, objectiveId) = await SeedEventWithObjectiveAsync(owner.Id, ownerKey);
        await TestAuth.CreateAuthenticatedClient(Factory, ownerKey)
            .PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });

        var compClient = TestAuth.CreateAuthenticatedClient(Factory, competitorKey);
        var url = $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete";

        (await compClient.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await compClient.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task FailRemainingObjectives_AsCompetitor_Returns200WithTheCounts()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eventGameId, objectiveId) = await SeedEventWithObjectiveAsync(owner.Id, ownerKey);
        await TestAuth.CreateAuthenticatedClient(Factory, ownerKey)
            .PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });
        var compClient = TestAuth.CreateAuthenticatedClient(Factory, competitorKey);
        await compClient.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete", null);

        var response = await compClient.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/fail-remaining", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FailRemainingDto>();
        body.Should().Be(new FailRemainingDto(FailedCount: 0, AlreadyCompletedCount: 1, AlreadyFailedCount: 0, TotalObjectives: 1));
    }

    [Fact]
    public async Task FailRemainingObjectives_NotCompetitor_Returns403()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, strangerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "stranger");
        var (ev, eventGameId, _) = await SeedEventWithObjectiveAsync(owner.Id, ownerKey);

        var response = await TestAuth.CreateAuthenticatedClient(Factory, strangerKey).PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/fail-remaining", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetScores_ReturnsTheAggregatedPayloadShape()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eventGameId, objectiveId) = await SeedEventWithObjectiveAsync(owner.Id, ownerKey, score: 25);
        await TestAuth.CreateAuthenticatedClient(Factory, ownerKey)
            .PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });
        await TestAuth.CreateAuthenticatedClient(Factory, competitorKey)
            .PostAsync($"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete", null);

        var response = await Client.GetAsync($"/api/v1/events/{ev.Id}/scores");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await response.Content.ReadFromJsonAsync<List<ScoreEntryDto>>();
        entries.Should().ContainSingle(e => e.UserId == competitor.Id && e.TotalScore == 25 && e.CompletedCount == 1);
    }

    [Fact]
    public async Task EditCompletionTime_AcceptsZuluOffsetAndZonelessTimestamps()
    {
        // The zone-less form must no longer 500. What instant each form
        // resolves to is ObjectiveWriteTests' subject; this proves only that
        // the JSON binder accepts all three, which only happens over the wire.
        foreach (var suffix in new[] { "Z", "+02:00", "" })
        {
            var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
            var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
            var (ev, eventGameId, objectiveId) = await SeedEventWithObjectiveAsync(owner.Id, ownerKey);
            var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
            await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });
            await TestAuth.CreateAuthenticatedClient(Factory, competitorKey)
                .PostAsync($"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete", null);

            // Captured after seeding, with a margin so truncating to whole
            // seconds can't land before ev.CreatedAt's own sub-second value.
            var target = DateTime.UtcNow.AddSeconds(2);
            var wallClock = suffix == "+02:00" ? target.AddHours(2) : target;
            var completedAt = wallClock.ToString("yyyy-MM-ddTHH:mm:ss") + suffix;

            var response = await ownerClient.PatchAsJsonAsync(
                $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/completions/{competitor.Id}",
                new { completedAt, reason = "backfilled from stream VOD" });

            response.StatusCode.Should().Be(HttpStatusCode.OK, $"format '{completedAt}' should be accepted");
        }
    }

    private async Task<(Event Event, Guid EventGameId, Guid ObjectiveId)> SeedEventWithObjectiveAsync(
        Guid ownerId, string ownerKey, int score = 10)
    {
        // Seeded unstarted so the game can be added through the API (adding
        // games to a running event is a 409). The event is then started before
        // the game is enabled, since EnableEventGame requires the opposite: the
        // event must already be running.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "test", CreatedById = ownerId };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var addResp = await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/games", new { gameId = SeededGameId });
        var eventGameId = (await addResp.Content.ReadFromJsonAsync<CreatedGameResponse>())!.Id;

        ev.IsStarted = true;
        await db.SaveChangesAsync();

        await ownerClient.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGameId}/enable", null);

        var create = await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/",
            new { name = "obj", score });
        var created = await create.Content.ReadFromJsonAsync<ObjDto>();
        return (ev, eventGameId, created!.Id);
    }

    private record ObjDto(Guid Id, string Name, int Score);
    private record FailRemainingDto(int FailedCount, int AlreadyCompletedCount, int AlreadyFailedCount, int TotalObjectives);
    private record ScoreEntryDto(Guid UserId, string DisplayName, int TotalScore, int CompletedCount);
    private record CreatedGameResponse(Guid Id);
}
