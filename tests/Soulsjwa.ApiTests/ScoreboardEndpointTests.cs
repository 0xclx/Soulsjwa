using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The scoreboard's HTTP contract: the route is anonymous, the decommissioned
/// one stays gone, the payload shape the scoreboard and overlay read, and
/// setting a competitor live goes through. Ordering, ranking, tie-break modes
/// and the terminal-status rule live in
/// <c>Soulsjwa.IntegrationTests.ScoreboardTests</c>.
/// </summary>
public class ScoreboardEndpointTests : ApiTestBase
{
    private const int SeededGameId = 1;

    [Fact]
    public async Task GetScoreboard_UnknownEvent_Returns404()
    {
        var response = await Client.GetAsync($"/api/v1/events/{Guid.NewGuid()}/scoreboard");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetScoreboard_DecommissionedRoute_Returns404()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner-old-route");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "old-route", CreatedById = owner.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/v1/events/{ev.Id}/leaderboard");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetScoreboard_ReturnsPerGameBreakdown()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eventGameId, objectiveId) = await SeedEventWithObjectiveAsync(owner.Id, ownerKey, score: 15);

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });

        var compClient = TestAuth.CreateAuthenticatedClient(Factory, competitorKey);
        await compClient.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete", null);

        var response = await Client.GetAsync($"/api/v1/events/{ev.Id}/scoreboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ScoreboardDto>();

        result!.Entries.Should().ContainSingle();
        var entry = result.Entries[0];
        entry.UserId.Should().Be(competitor.Id);
        entry.TotalScore.Should().Be(15);
        entry.CompletedCount.Should().Be(1);
        entry.TwitchLogin.Should().NotBeNullOrEmpty();
        entry.Games.Should().ContainSingle();
        entry.Games[0].Score.Should().Be(15);
        entry.Games[0].Objectives.Should().ContainSingle(o => o.IsCompleted);
    }

    [Fact]
    public async Task GetScoreboard_IsAnonymous()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "anon-test", CreatedById = owner.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/v1/events/{ev.Id}/scoreboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetLive_AsCompetitor_Returns204()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "live-set", CreatedById = owner.Id };
        db.Events.Add(ev);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = competitor.Id });
        await db.SaveChangesAsync();

        var compClient = TestAuth.CreateAuthenticatedClient(Factory, competitorKey);
        var response = await compClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/live", new { isLive = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
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
    private record CreatedGameResponse(Guid Id);
    private record ScoreboardDto(List<ScoreboardEntryDto> Entries, string TieBreakMode);
    private record ScoreboardEntryDto(
        Guid UserId, string DisplayName, string TwitchLogin, string? ProfileImageUrl,
        bool IsLive, int TotalScore, int CompletedCount, bool IsFinished,
        DateTime? LastCompletedAt, long? TotalInGameTimeMs, int Rank,
        List<GameBreakdownDto> Games, int FailedCount = 0,
        string Status = nameof(ObjectiveOutcome.Pending));
    private record GameBreakdownDto(
        Guid EventGameId, string GameName, int Score, int CompletedCount,
        int TotalObjectives, List<ObjectiveDetailDto> Objectives);
    private record ObjectiveDetailDto(
        Guid ObjectiveId, string Name, int Score, string? Category, bool IsCompleted, DateTime? CompletedAt);
}
