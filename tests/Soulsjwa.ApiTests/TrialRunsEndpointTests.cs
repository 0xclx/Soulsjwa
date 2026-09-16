using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The trial-run endpoints' HTTP contract: each route exists, the competitor
/// gate is wired in, enable answers 201 then 200 with the same run, and the
/// scoreboard payload carries trial figures in the shape the frontend reads.
/// Plus one connector submission, since that path authenticates differently.
/// The rules (kill switch, archived-event gate, state-machine effects, disable
/// cascade, expected-run guard, the one-way boundary between practice and the
/// real record) live in <c>Soulsjwa.IntegrationTests.TrialRunTests</c>.
/// </summary>
public class TrialRunsEndpointTests : ApiTestBase
{
    private const int ConnectorSupportedGameId = 1; // Dark Souls: Remastered, seeded connector-supported
    private const int ConnectorFlagId = 16;          // Asylum Demon
    private static readonly string ConnectorFlagDataPointId =
        GameDataDefinitions.SoulMemoryFlagId(ConnectorSupportedGameId, ConnectorFlagId);

    [Fact]
    public async Task EnableTrialRun_NotACompetitor_Returns403()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var (ev, eventGame) = await SeedEventWithGameAsync(owner.Id, allowTrialRuns: true);
        // Owner never joins as a competitor.

        using var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var response = await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EnableTrialRun_Idempotent_ReturnsSameRunOn200()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var (ev, eventGame) = await SeedEventWithGameAsync(owner.Id, allowTrialRuns: true);
        await AddCompetitorAsync(ev.Id, owner.Id);

        using var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var first = await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}", null);
        var firstBody = await first.Content.ReadFromJsonAsync<TrialRunDto>();

        var second = await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}", null);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<TrialRunDto>();

        secondBody!.Id.Should().Be(firstBody!.Id);
    }

    [Fact]
    public async Task StartStopReset_TransitionStateCorrectly()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var (ev, eventGame) = await SeedEventWithGameAsync(owner.Id, allowTrialRuns: true);
        await AddCompetitorAsync(ev.Id, owner.Id);

        using var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}", null);

        var started = await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}/start", null);
        (await started.Content.ReadFromJsonAsync<TrialRunDto>())!.State.Should().Be(nameof(TrialRunState.Running));

        var stopped = await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}/stop", null);
        (await stopped.Content.ReadFromJsonAsync<TrialRunDto>())!.State.Should().Be(nameof(TrialRunState.Paused));

        var reset = await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}/reset", null);
        var resetBody = await reset.Content.ReadFromJsonAsync<TrialRunDto>();
        resetBody!.State.Should().Be(nameof(TrialRunState.NotStarted));
        resetBody.StartedAt.Should().BeNull();
    }

    [Fact]
    public async Task TrialCompletion_DoesNotAppearOnScoreboard()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var (ev, eventGame) = await SeedEventWithGameAsync(owner.Id, allowTrialRuns: true);
        await AddCompetitorAsync(ev.Id, owner.Id);

        using var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var objResp = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/objectives/",
            new { name = "Trial objective", score = 5 });
        var objectiveId = (await objResp.Content.ReadFromJsonAsync<ObjectiveDto>())!.Id;

        await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}", null);
        await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}/start", null);

        var completeResp = await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/objectives/{objectiveId}/complete", null);
        completeResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var scoreboard = await client.GetFromJsonAsync<ScoreboardDto>($"/api/v1/events/{ev.Id}/scoreboard");
        var entry = scoreboard!.Entries.Single(e => e.UserId == owner.Id);
        entry.TotalScore.Should().Be(0);
        entry.CompletedCount.Should().Be(0);

        // ...but it is reported separately, so the scoreboard and overlay can
        // show what the practice run has actually earned.
        var game = entry.Games.Single(g => g.EventGameId == eventGame.Id);
        game.Score.Should().Be(0);
        game.CompletedCount.Should().Be(0);
        game.IsTrialActive.Should().BeTrue();
        game.Trial.Should().NotBeNull();
        game.Trial!.State.Should().Be(nameof(TrialRunState.Running));
        game.Trial.Score.Should().Be(5);
        game.Trial.CompletedCount.Should().Be(1);
        game.Trial.FailedCount.Should().Be(0);
        game.Trial.LastCompletedAt.Should().NotBeNull();

        var objective = game.Objectives.Single(o => o.ObjectiveId == objectiveId);
        objective.IsCompleted.Should().BeFalse();
        objective.Trial.Should().NotBeNull();
        objective.Trial!.IsCompleted.Should().BeTrue();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var completion = await db.CompletedObjectives.SingleAsync(c => c.ObjectiveId == objectiveId);
        completion.TrialRunId.Should().NotBeNull();
    }

    [Fact]
    public async Task ConnectorSubmission_DuringActiveTrial_BypassesDisabledGameGate()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var (ev, eventGame) = await SeedEventWithGameAsync(
            owner.Id, allowTrialRuns: true, enableGame: false, knownGameId: ConnectorSupportedGameId);
        await AddCompetitorAsync(ev.Id, owner.Id);

        using var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGame.Id}/objectives/",
            new
            {
                name = "Reach checkpoint",
                score = 5,
                rule = $$"""{">":[{"var":"{{ConnectorFlagDataPointId}}"},0]}""",
            });

        // Without an active trial, submitting to this disabled game is refused.
        var refused = await client.PostAsJsonAsync(
            $"/api/v1/connector/events/{ev.Id}/games/{eventGame.Id}/submit",
            new { data = $$"""{"{{ConnectorFlagDataPointId}}":1}""" });
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}", null);
        await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGame.Id}/trial-runs/{owner.Id}/start", null);

        var submitted = await client.PostAsJsonAsync(
            $"/api/v1/connector/events/{ev.Id}/games/{eventGame.Id}/submit",
            new { data = $$"""{"{{ConnectorFlagDataPointId}}":1}""" });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await submitted.Content.ReadFromJsonAsync<SubmitResultDto>())!.CompletedCount.Should().Be(1);
    }

    private async Task<(Event Event, EventGame EventGame)> SeedEventWithGameAsync(
        Guid ownerId, bool allowTrialRuns, bool enableGame = true, int? knownGameId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "trial-test event", CreatedById = ownerId, IsStarted = true, AllowTrialRuns = allowTrialRuns };
        var game = new EventGame
        {
            KnownGameId = knownGameId,
            CustomGameName = knownGameId is null ? "Custom Game" : null,
            IsEnabled = enableGame,
        };
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

    private sealed record TrialRunDto(Guid Id, Guid EventId, Guid EventGameId, Guid UserId, string State, DateTime? StartedAt, DateTime? EndedAt);
    private sealed record ObjectiveDto(Guid Id);
    private sealed record ScoreboardDto(List<ScoreEntryDto> Entries, string TieBreakMode);
    private sealed record ScoreEntryDto(
        Guid UserId, int TotalScore, int CompletedCount, int Rank, List<GameBreakdownDto> Games);
    private sealed record GameBreakdownDto(
        Guid EventGameId, int Score, int CompletedCount, bool IsEnabled, bool IsTrialActive,
        List<ObjectiveDetailDto> Objectives, TrialProgressDto? Trial);
    private sealed record ObjectiveDetailDto(
        Guid ObjectiveId, bool IsCompleted, string Status, TrialObjectiveStateDto? Trial);
    private sealed record TrialObjectiveStateDto(bool IsCompleted, bool IsFailed, string Status);
    private sealed record TrialProgressDto(
        Guid TrialRunId, string State, int Score, int CompletedCount, int FailedCount, DateTime? LastCompletedAt);
    private sealed record SubmitResultDto(int CompletedCount, int FailedCount);
}
