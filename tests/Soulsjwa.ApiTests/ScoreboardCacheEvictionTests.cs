using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The scoreboard cache tag must be scoped per event — a write in one event
/// must not evict another event's cached scoreboard.
/// </summary>
public class ScoreboardCacheEvictionTests : ApiTestBase
{
    private const int SeededGameId = 1;

    [Fact]
    public async Task CompletionInOneEvent_DoesNotEvictAnotherEvents_CachedScores()
    {
        var a = await SeedEventWithCompetitorAsync("a");
        var b = await SeedEventWithCompetitorAsync("b");

        // Warm both caches.
        var firstB = await Client.GetFromJsonAsync<List<ScoreEntryDto>>($"/api/v1/events/{b.Event.Id}/scores");
        await Client.GetFromJsonAsync<List<ScoreEntryDto>>($"/api/v1/events/{a.Event.Id}/scores");

        // Mutate event B directly in the DB (bypassing the API, so nothing
        // evicts its cache) to make a cache hit observable.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == b.Competitor.Id);
            user.DisplayName = "mutated-b";
            await db.SaveChangesAsync();
        }

        // Complete an objective in event A via the real endpoint — this must
        // only evict event A's tag.
        var competitorAClient = TestAuth.CreateAuthenticatedClient(Factory, a.CompetitorKey);
        var complete = await competitorAClient.PostAsync(
            $"/api/v1/events/{a.Event.Id}/games/{a.EventGameId}/objectives/{a.ObjectiveId}/complete", null);
        complete.EnsureSuccessStatusCode();

        var secondA = await Client.GetFromJsonAsync<List<ScoreEntryDto>>($"/api/v1/events/{a.Event.Id}/scores");
        secondA!.Single(e => e.UserId == a.Competitor.Id).CompletedCount.Should().Be(1,
            "event A's own cache entry must have been evicted by its own write");

        var secondB = await Client.GetFromJsonAsync<List<ScoreEntryDto>>($"/api/v1/events/{b.Event.Id}/scores");
        secondB!.Single(e => e.UserId == b.Competitor.Id).DisplayName.Should().Be(
            firstB!.Single(e => e.UserId == b.Competitor.Id).DisplayName,
            "event B's cache entry must still be served — a write in event A must not have evicted it");
    }

    private async Task<SeededEvent> SeedEventWithCompetitorAsync(string suffix)
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, $"owner-{suffix}");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, $"comp-{suffix}");

        // Seeded unstarted so the game can be added through the API (adding
        // games to a running event is a 409). The event is then started before
        // the game is enabled, since EnableEventGame requires the opposite: the
        // event must already be running.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = $"cache-isolation-{suffix}", CreatedById = owner.Id };
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
            new { name = "obj", score = 10 });
        var created = await create.Content.ReadFromJsonAsync<ObjDto>();

        await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });

        return new SeededEvent(ev, eventGameId, created!.Id, competitor, competitorKey);
    }

    private sealed record SeededEvent(
        Event Event,
        Guid EventGameId,
        Guid ObjectiveId,
        Soulsjwa.Api.Features.Auth.Entities.User Competitor,
        string CompetitorKey);

    private record ObjDto(Guid Id);
    private record CreatedGameResponse(Guid Id);
    private record ScoreEntryDto(Guid UserId, string DisplayName, int CompletedCount);
}
