using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The Trial tab's two routes over the wire, with the payload field names the
/// tab reads. Who may see whose run, and what the objectives read reports, live
/// in <c>Soulsjwa.IntegrationTests.TrialRunTests</c>.
/// </summary>
public class MyTrialRunsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task List_ReturnsOwnTrialRunWithItsProgress()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var (ev, eventGame) = await SeedEventWithGameAsync(owner.Id);
        await AddCompetitorAsync(ev.Id, owner.Id);

        using var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var objectiveId = await CreateObjectiveAsync(client, ev.Id, eventGame.Id, score: 20);
        await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}", null);
        await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}/start", null);
        await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/objectives/{objectiveId}/complete", null);

        var runs = await client.GetFromJsonAsync<List<MyTrialRunDto>>("/api/v1/me/trial-runs/");

        var run = runs!.Single();
        run.EventId.Should().Be(ev.Id);
        run.EventGameId.Should().Be(eventGame.Id);
        run.CompetitorId.Should().Be(owner.Id);
        run.IsOwnTrial.Should().BeTrue();
        run.State.Should().Be(nameof(TrialRunState.Running));
        run.Score.Should().Be(20);
        run.CompletedCount.Should().Be(1);
        run.TotalObjectives.Should().Be(1);
    }

    [Fact]
    public async Task GetObjectives_ReportsTheRunsOwnRowsAndRefusesAStranger()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (stranger, strangerKey) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "stranger", role: UserRole.User);
        var (ev, eventGame) = await SeedEventWithGameAsync(owner.Id);
        await AddCompetitorAsync(ev.Id, owner.Id);
        await AddCompetitorAsync(ev.Id, stranger.Id);

        using var client = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var completedId = await CreateObjectiveAsync(client, ev.Id, eventGame.Id, name: "Done", score: 5);
        var pendingId = await CreateObjectiveAsync(client, ev.Id, eventGame.Id, name: "Pending", score: 5);

        var enable = await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}", null);
        var trialRunId = (await enable.Content.ReadFromJsonAsync<TrialRunIdDto>())!.Id;
        await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}/start", null);
        await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/objectives/{completedId}/complete", null);

        var objectives = await client.GetFromJsonAsync<MyEventObjectivesDto>(
            $"/api/v1/me/trial-runs/{trialRunId}/objectives");

        var game = objectives!.Games.Single();
        game.GameId.Should().Be(eventGame.Id);
        game.Objectives.Single(o => o.ObjectiveId == completedId).Completed.Should().BeTrue();
        game.Objectives.Single(o => o.ObjectiveId == pendingId).Completed.Should().BeFalse();

        using var strangerClient = TestAuth.CreateAuthenticatedClient(Factory, strangerKey);
        var refused = await strangerClient.GetAsync($"/api/v1/me/trial-runs/{trialRunId}/objectives");
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> CreateObjectiveAsync(
        HttpClient client, Guid eventId, Guid eventGameId, string name = "Objective", int score = 10)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{eventId}/games/{eventGameId}/objectives/", new { name, score });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ObjectiveDto>())!.Id;
    }

    private async Task<(Event Event, EventGame EventGame)> SeedEventWithGameAsync(Guid ownerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event
        {
            Name = "my-trial-runs event",
            CreatedById = ownerId,
            IsStarted = true,
            AllowTrialRuns = true,
        };
        var game = new EventGame { CustomGameName = "Custom Game", IsEnabled = true };
        ev.EventGames.Add(game);
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (ev, game);
    }

    private async Task AddCompetitorAsync(Guid eventId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EventCompetitors.Add(new EventCompetitor { EventId = eventId, UserId = userId });
        await db.SaveChangesAsync();
    }

    private sealed record ObjectiveDto(Guid Id);
    private sealed record TrialRunIdDto(Guid Id);
    private sealed record MyTrialRunDto(
        Guid TrialRunId, Guid EventId, string EventName, Guid EventGameId, string GameName,
        bool IsGameEnabled, Guid CompetitorId, string CompetitorName, bool IsOwnTrial,
        string State, int Score, int CompletedCount, int FailedCount, int TotalObjectives);
    private sealed record MyEventObjectivesDto(Guid CompetitorId, List<MyEventGameDto> Games);
    private sealed record MyEventGameDto(
        Guid GameId, string GameName, List<MyEventObjectiveDto> Objectives, bool IsTrialActive);
    private sealed record MyEventObjectiveDto(Guid ObjectiveId, string Name, bool Completed, bool Failed);
}
