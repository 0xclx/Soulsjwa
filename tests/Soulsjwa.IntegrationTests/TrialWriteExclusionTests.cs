using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Official progress and a trial run are mutually exclusive per
/// (event game, competitor), and <see cref="TrialRunLookup"/> is the single
/// decision point every write surface routes through. Two failures guarded: a
/// dormant slot must yield <see cref="ObjectiveWriteScope.Blocked"/>, never
/// <see cref="ObjectiveWriteScope.Official"/> (the "I was in trial mode, why
/// did my real score move?" bug), and an existing official row must forbid a
/// trial.
/// </summary>
public class TrialWriteExclusionTests : IntegrationTestBase
{
    [Fact]
    public async Task WriteTarget_WithNoTrialSlot_IsOfficial()
    {
        var db = CreateDbContext();
        var (_, game, competitor) = await SeedAsync(db);

        var target = await TrialRunLookup.ResolveWriteTargetAsync(db, game.Id, competitor.Id, default);

        target.Scope.Should().Be(ObjectiveWriteScope.Official);
        target.TrialRunId.Should().BeNull();
        target.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task WriteTarget_WithARecordingRun_IsThatRun()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var run = await AddTrialRunAsync(db, ev, game, competitor, TrialRunState.Running);

        var target = await TrialRunLookup.ResolveWriteTargetAsync(db, game.Id, competitor.Id, default);

        target.Scope.Should().Be(ObjectiveWriteScope.Trial);
        target.TrialRunId.Should().Be(run.Id);
        target.IsBlocked.Should().BeFalse();
    }

    [Theory]
    [InlineData(TrialRunState.NotStarted)]
    [InlineData(TrialRunState.Paused)]
    [InlineData(TrialRunState.Completed)]
    public async Task WriteTarget_WithADormantRun_IsBlockedRatherThanOfficial(TrialRunState state)
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        await AddTrialRunAsync(db, ev, game, competitor, state);

        var target = await TrialRunLookup.ResolveWriteTargetAsync(db, game.Id, competitor.Id, default);

        target.IsBlocked.Should().BeTrue(
            "a slot that is not recording must refuse the write, not quietly make it official");
        target.Scope.Should().NotBe(ObjectiveWriteScope.Official);
        target.TrialRunId.Should().BeNull();
    }

    [Fact]
    public async Task WriteTarget_IsScopedToOneCompetitorAndOneGame()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var otherGame = new EventGame { EventId = ev.Id, CustomGameName = "Other" };
        var rival = new User { DisplayName = "Rival", TwitchLogin = "rival", TwitchId = "t9" };
        db.EventGames.Add(otherGame);
        db.Users.Add(rival);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = rival.Id });
        await db.SaveChangesAsync();

        // A rival practising the same game, and my own trial on a different
        // game, must both leave this game's official writes alone.
        await AddTrialRunAsync(db, ev, game, rival, TrialRunState.Running);
        await AddTrialRunAsync(db, ev, otherGame, competitor, TrialRunState.Running);

        var target = await TrialRunLookup.ResolveWriteTargetAsync(db, game.Id, competitor.Id, default);

        target.Scope.Should().Be(ObjectiveWriteScope.Official);
    }

    [Fact]
    public async Task OfficialOutcome_IsNotClaimedByTrialRowsAlone()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var objective = await AddObjectiveAsync(db, game);
        var run = await AddTrialRunAsync(db, ev, game, competitor, TrialRunState.Running);
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = run.Id,
        });
        db.FailedObjectives.Add(new FailedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = run.Id,
        });
        await db.SaveChangesAsync();

        // Otherwise resetting a trial would be the only way to ever restart
        // one, and a run could block its own resumption.
        var hasOfficial = await TrialRunLookup.HasOfficialOutcomeAsync(db, game.Id, competitor.Id, default);

        hasOfficial.Should().BeFalse();
    }

    [Fact]
    public async Task OfficialOutcome_IsClaimedByAnOfficialCompletion()
    {
        var db = CreateDbContext();
        var (_, game, competitor) = await SeedAsync(db);
        var objective = await AddObjectiveAsync(db, game);
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
        });
        await db.SaveChangesAsync();

        var hasOfficial = await TrialRunLookup.HasOfficialOutcomeAsync(db, game.Id, competitor.Id, default);

        hasOfficial.Should().BeTrue();
    }

    [Fact]
    public async Task OfficialOutcome_IsClaimedByAnOfficialFailureToo()
    {
        var db = CreateDbContext();
        var (_, game, competitor) = await SeedAsync(db);
        var objective = await AddObjectiveAsync(db, game);
        db.FailedObjectives.Add(new FailedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
        });
        await db.SaveChangesAsync();

        // A failed objective is official progress just as much as a completed
        // one: a trial beside it would make the real attempt unreadable.
        var hasOfficial = await TrialRunLookup.HasOfficialOutcomeAsync(db, game.Id, competitor.Id, default);

        hasOfficial.Should().BeTrue();
    }

    [Fact]
    public async Task OfficialOutcome_IgnoresOtherGamesAndOtherCompetitors()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var otherGame = new EventGame { EventId = ev.Id, CustomGameName = "Other" };
        var rival = new User { DisplayName = "Rival", TwitchLogin = "rival", TwitchId = "t9" };
        db.EventGames.Add(otherGame);
        db.Users.Add(rival);
        await db.SaveChangesAsync();

        var here = await AddObjectiveAsync(db, game);
        var elsewhere = await AddObjectiveAsync(db, otherGame);
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = elsewhere.Id,
            UserId = competitor.Id,
        });
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = here.Id,
            UserId = rival.Id,
        });
        await db.SaveChangesAsync();

        // My progress on another game, and a rival's on this one, are both
        // irrelevant to whether I may practise this game.
        var hasOfficial = await TrialRunLookup.HasOfficialOutcomeAsync(db, game.Id, competitor.Id, default);

        hasOfficial.Should().BeFalse();
    }

    private static async Task<(Event Event, EventGame Game, User Competitor)> SeedAsync(AppDbContext db)
    {
        var owner = new User { DisplayName = "Owner", TwitchLogin = "owner", TwitchId = "t1" };
        db.Users.Add(owner);
        var ev = new Event { Name = "Event", CreatedById = owner.Id, IsStarted = true };
        var game = new EventGame { CustomGameName = "Custom Game", IsEnabled = true };
        ev.EventGames.Add(game);
        db.Events.Add(ev);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = owner.Id });
        await db.SaveChangesAsync();
        return (ev, game, owner);
    }

    private static async Task<Objective> AddObjectiveAsync(
        AppDbContext db, EventGame game, string name = "Objective", int score = 10)
    {
        var objective = new Objective { EventGameId = game.Id, Name = name, Score = score };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();
        return objective;
    }

    private static async Task<TrialRun> AddTrialRunAsync(
        AppDbContext db, Event ev, EventGame game, User user, TrialRunState state)
    {
        var trialRun = new TrialRun
        {
            EventId = ev.Id,
            EventGameId = game.Id,
            UserId = user.Id,
            State = state,
            StartedAt = state == TrialRunState.NotStarted ? null : DateTime.UtcNow,
        };
        db.TrialRuns.Add(trialRun);
        await db.SaveChangesAsync();
        return trialRun;
    }
}
