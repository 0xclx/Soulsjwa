using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Trial runs and the one-way boundary around them: what a competitor does
/// inside a trial must never alter their official record, their standing, or
/// anybody else's, and a trial must never sit beside real progress on the same
/// game. Mostly enforced by Postgres — one slot per (game, competitor) is a
/// unique index, official and trial rows are kept apart by partial unique
/// indexes, and disabling a trial relies on a cascade. The wire contract is
/// <c>Soulsjwa.ApiTests.TrialRunsEndpointTests</c>.
/// </summary>
public class TrialRunTests : IntegrationTestBase
{
    /// <summary>Fails a competitor as soon as one other competitor completes the objective.</summary>
    private const string CountOnlyFailRule = """{">=":[{"var":"competitorCompletions"},1]}""";

    [Fact]
    public async Task Enable_WhenTheOwnerHasNotAllowedTrials_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: false);

        var result = await EnableAsync(db, f);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Enable_ForSomeoneWhoIsNotACompetitor_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true, withCompetitor: false);

        var result = await EnableAsync(db, f);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Enable_OnAnArchivedEvent_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        f.Event.IsArchived = true;
        await db.SaveChangesAsync();

        var result = await EnableAsync(CreateDbContext(), f);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// The archived 403 above is for members. To anyone else an archived
    /// event must look exactly like a missing one, as on every other route —
    /// otherwise the trial routes confirm which ids exist.
    /// </summary>
    [Fact]
    public async Task Enable_OnAnArchivedEvent_ByANonMember_IsNotFound()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        f.Event.IsArchived = true;
        await db.SaveChangesAsync();
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await TrialRunsEndpoint.EnableTrialRun(
            f.Event.Id, f.Game.Id, stranger.Id, stranger.Principal(), CreateDbContext(), Audit, Cache, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Enable_OnAGameThatIsNotTheActiveOne_Succeeds()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true, enabled: false);

        var result = await EnableAsync(db, f);

        result.Status().Should().Be(StatusCodes.Status201Created,
            "practice is not restricted to whatever the event is currently running");
    }

    [Fact]
    public async Task Enable_Twice_ReturnsTheSameSlot()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);

        var first = await EnableAsync(db, f);
        var second = await EnableAsync(CreateDbContext(), f);

        first.Status().Should().Be(StatusCodes.Status201Created);
        second.Status().Should().Be(StatusCodes.Status200OK);
        second.Value<TrialRunResponse>().Id.Should().Be(first.Value<TrialRunResponse>().Id);
        (await CreateDbContext().TrialRuns.CountAsync(t => t.EventGameId == f.Game.Id)).Should().Be(1);
    }

    [Fact]
    public async Task StartStopReset_WalkTheStateMachine()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        await EnableAsync(db, f);

        (await StartAsync(CreateDbContext(), f)).Value<TrialRunResponse>()
            .State.Should().Be(nameof(TrialRunState.Running));
        (await StopAsync(CreateDbContext(), f)).Value<TrialRunResponse>()
            .State.Should().Be(nameof(TrialRunState.Paused));
        (await StartAsync(CreateDbContext(), f)).Value<TrialRunResponse>()
            .State.Should().Be(nameof(TrialRunState.Running));
        (await ResetAsync(CreateDbContext(), f)).Value<TrialRunResponse>()
            .State.Should().Be(nameof(TrialRunState.NotStarted));
    }

    [Fact]
    public async Task Start_AfterTheOwnerRevokesTrials_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        await EnableAsync(db, f);

        // The kill switch has to be re-checked on start, not only on enable:
        // otherwise a slot created while trials were allowed stays startable
        // forever.
        var revoke = CreateDbContext();
        (await revoke.Events.SingleAsync(e => e.Id == f.Event.Id)).AllowTrialRuns = false;
        await revoke.SaveChangesAsync();

        var result = await StartAsync(CreateDbContext(), f);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Reset_ClearsTheTrialProgressButKeepsTheSlot()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        await EnableAsync(db, f);
        await StartAsync(CreateDbContext(), f);
        await CompleteAsync(CreateDbContext(), f);

        // Pausing keeps it; resetting is the destructive one.
        await StopAsync(CreateDbContext(), f);
        (await CreateDbContext().CompletedObjectives.CountAsync(c => c.TrialRunId != null))
            .Should().Be(1, "a paused run keeps what it recorded");

        await ResetAsync(CreateDbContext(), f);

        var after = CreateDbContext();
        (await after.CompletedObjectives.CountAsync(c => c.TrialRunId != null)).Should().Be(0);
        (await after.TrialRuns.CountAsync(t => t.EventGameId == f.Game.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Disable_DeletesOnlyThatCompetitorsRowsForThatGame()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        var other = await Fixtures.AddUserAsync(db, "other");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = other.Id });
        await db.SaveChangesAsync();

        await EnableAsync(db, f);
        await StartAsync(CreateDbContext(), f);
        await CompleteAsync(CreateDbContext(), f);

        var otherRun = await Fixtures.AddTrialRunAsync(CreateDbContext(), f with { Competitor = other });
        var otherDb = CreateDbContext();
        otherDb.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = f.Objective.Id,
            UserId = other.Id,
            TrialRunId = otherRun.Id,
        });
        await otherDb.SaveChangesAsync();

        var result = await DisableAsync(CreateDbContext(), f);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        var after = CreateDbContext();
        (await after.TrialRuns.AnyAsync(t => t.UserId == f.Competitor.Id)).Should().BeFalse();
        (await after.CompletedObjectives.AnyAsync(c => c.UserId == f.Competitor.Id)).Should().BeFalse(
            "the cascade takes the completions recorded under the deleted run");
        (await after.TrialRuns.AnyAsync(t => t.Id == otherRun.Id)).Should().BeTrue(
            "another competitor's practice is none of this request's business");
        (await after.CompletedObjectives.AnyAsync(c => c.UserId == other.Id)).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Enable_OnceOfficialProgressExists_Conflicts(bool completedRatherThanFailed)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 25, allowTrialRuns: true);
        if (completedRatherThanFailed)
            await CompleteAsync(db, f);
        else
            await FailAsync(db, f);

        var enable = await EnableAsync(CreateDbContext(), f);

        enable.Status().Should().Be(StatusCodes.Status409Conflict,
            "a practice run must not sit beside a real attempt on the same game");
        enable.Detail().Should().Be(TrialRunLookup.OfficialProgressDetail);

        // No slot was created, so there is nothing to start either.
        (await StartAsync(CreateDbContext(), f)).Status().Should().Be(StatusCodes.Status404NotFound);
        (await CreateDbContext().TrialRuns.CountAsync(t => t.EventGameId == f.Game.Id)).Should().Be(0);
    }

    [Theory]
    [InlineData(TrialRunState.NotStarted)]
    [InlineData(TrialRunState.Paused)]
    public async Task ADormantTrial_RefusesOfficialWritesRatherThanTakingThem(TrialRunState state)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 25, allowTrialRuns: true);
        await Fixtures.AddTrialRunAsync(db, f, state);

        // The state that used to send ticks straight into the official record
        // while the UI showed a trial chip.
        var complete = await CompleteAsync(CreateDbContext(), f);
        var fail = await FailAsync(CreateDbContext(), f);

        complete.Status().Should().Be(StatusCodes.Status409Conflict);
        complete.Detail().Should().Be(TrialRunLookup.BlockedDetail);
        fail.Status().Should().Be(StatusCodes.Status409Conflict);

        var after = CreateDbContext();
        (await after.CompletedObjectives.CountAsync(c => c.ObjectiveId == f.Objective.Id)).Should().Be(0);
        (await after.FailedObjectives.CountAsync(x => x.ObjectiveId == f.Objective.Id)).Should().Be(0);
    }

    [Fact]
    public async Task DisablingTheTrial_IsTheEscapeHatchAndWorksImmediately()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 25, allowTrialRuns: true);
        await EnableAsync(db, f);
        (await CompleteAsync(CreateDbContext(), f)).Status().Should().Be(StatusCodes.Status409Conflict);

        await DisableAsync(CreateDbContext(), f);

        (await CompleteAsync(CreateDbContext(), f)).Status().Should().Be(StatusCodes.Status201Created);
        await AssertOfficialAsync(f, expectedScore: 25, expectedCompleted: 1);
    }

    [Fact]
    public async Task ATrialCompletion_StaysOutOfTheOfficialScore()
    {
        var f = await RecordingTrialAsync(score: 30);

        await CompleteAsync(CreateDbContext(), f);

        var written = await CreateDbContext().CompletedObjectives
            .SingleAsync(c => c.ObjectiveId == f.Objective.Id);
        written.TrialRunId.Should().NotBeNull();
        await AssertOfficialAsync(f, expectedScore: 0, expectedCompleted: 0);
    }

    [Fact]
    public async Task ATrialCompletion_DoesNotCascadeAFailureOntoAnybodyElse()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        var rival = await Fixtures.AddUserAsync(db, "rival");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = rival.Id });
        var objective = new Objective
        {
            EventGameId = f.Game.Id,
            Name = "Race objective",
            Score = 10,
            FailRule = CountOnlyFailRule,
        };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();
        var raced = f with { Objective = objective };

        await EnableAsync(CreateDbContext(), raced);
        await StartAsync(CreateDbContext(), raced);
        (await CompleteAsync(CreateDbContext(), raced)).Status().Should().Be(StatusCodes.Status201Created);

        (await CreateDbContext().FailedObjectives.AnyAsync(x => x.UserId == rival.Id)).Should().BeFalse(
            "practising must not burn anyone else's run");

        // Control: recorded officially, the same completion *does* cascade —
        // so the assertion above is about the trial, not a dud fail rule.
        // Trial mode has to be disabled rather than merely stopped, since a
        // dormant slot refuses the official write instead of taking it.
        await DisableAsync(CreateDbContext(), raced);
        (await CompleteAsync(CreateDbContext(), raced)).Status().Should().Be(StatusCodes.Status201Created);

        var rivalFailure = await CreateDbContext().FailedObjectives.SingleAsync(x => x.UserId == rival.Id);
        rivalFailure.TrialRunId.Should().BeNull();
    }

    [Fact]
    public async Task Uncomplete_DuringATrial_TakesTheTrialRowNotAnOfficialOne()
    {
        var f = await RecordingTrialAsync(score: 25);
        await CompleteAsync(CreateDbContext(), f);
        (await CreateDbContext().CompletedObjectives.SingleAsync(c => c.ObjectiveId == f.Objective.Id))
            .TrialRunId.Should().NotBeNull("the tick belonged to the recording run");

        var result = await CompletedObjectivesEndpoint.UncompleteObjective(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Principal(),
            CreateDbContext(), Audit, Cache, NullLogger<CompletedObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await CreateDbContext().CompletedObjectives.CountAsync(c => c.ObjectiveId == f.Objective.Id))
            .Should().Be(0);
        await AssertOfficialAsync(f, expectedScore: 0, expectedCompleted: 0);
    }

    [Fact]
    public async Task ATrialFailure_LeavesTheObjectivePendingOfficially()
    {
        var f = await RecordingTrialAsync();

        await FailAsync(CreateDbContext(), f);

        var entry = await OfficialEntryAsync(f);
        entry.FailedCount.Should().Be(0);
        entry.Status.Should().Be(nameof(ObjectiveOutcome.Pending));

        var game = entry.Games.Single(g => g.EventGameId == f.Game.Id);
        game.FailedCount.Should().Be(0);
        game.Trial!.FailedCount.Should().Be(1);

        var objective = game.Objectives.Single(o => o.ObjectiveId == f.Objective.Id);
        objective.IsFailed.Should().BeFalse();
        objective.Trial!.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ACleanSweepInsideATrial_DoesNotFinishTheCompetitor()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 5, allowTrialRuns: true);
        var second = await Fixtures.AddObjectiveAsync(db, f.Game, "Second", 5);
        await EnableAsync(CreateDbContext(), f);
        await StartAsync(CreateDbContext(), f);

        await CompleteAsync(CreateDbContext(), f);
        await CompleteAsync(CreateDbContext(), f with { Objective = second });

        var entry = await OfficialEntryAsync(f);
        entry.IsFinished.Should().BeFalse("a clean-sweep practice run is still a practice run");
        entry.Status.Should().Be(nameof(ObjectiveOutcome.Pending));
        entry.TotalScore.Should().Be(0);
        entry.Games.Single(g => g.EventGameId == f.Game.Id).Trial!.Score.Should().Be(10);
    }

    [Fact]
    public async Task ATrialCompletion_ReachesNeitherTheScoresReadNorMyEvents()
    {
        var f = await RecordingTrialAsync(score: 30);
        await CompleteAsync(CreateDbContext(), f);

        var scores = await CompletedObjectivesEndpoint.GetScores(f.Event.Id, CreateDbContext(), default);
        scores.Value<IEnumerable<ScoreEntry>>()
            .Single(s => s.UserId == f.Competitor.Id).TotalScore.Should().Be(0);

        var myEvents = await MyEventsEndpoint.List(f.Competitor.Principal(), CreateDbContext(), default);
        myEvents.Value<MyEventsResponse>()
            .Competitor.Single(e => e.EventId == f.Event.Id).Score.Should().Be(0);

        // The regular objective list stays official too — trial progress is
        // reported only where it is explicitly asked for, and the game is
        // flagged so the UI can point at the Trial tab.
        var objectives = await MyEventsEndpoint.GetObjectives(
            f.Event.Id, f.Competitor.Principal(), CreateDbContext(), default);
        var game = objectives.Value<MyEventObjectivesResponse>()
            .Games.Single(g => g.GameId == f.Game.Id);
        game.Objectives.Single(o => o.ObjectiveId == f.Objective.Id).Completed.Should().BeFalse();
        game.IsTrialActive.Should().BeTrue();
        game.HasTrialRun.Should().BeTrue();
    }

    [Fact]
    public async Task TrialProgress_IsNotReportedBeforeTheRunStarts()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        await EnableAsync(db, f);

        var entry = await OfficialEntryAsync(f);

        entry.Games.Single(g => g.EventGameId == f.Game.Id).Trial.Should().BeNull(
            "an untouched slot has nothing to report");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AWriteInsideARecordingTrial_IsAcceptedEvenOnADisabledGame(bool complete)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 100, allowTrialRuns: true, enabled: false);
        await EnableAsync(db, f);
        await StartAsync(CreateDbContext(), f);

        // The game-enabled gate exists to stop official scoring on a game the
        // event is not running. Practice is not scoring, so it must pass.
        var result = complete
            ? await CompleteAsync(CreateDbContext(), f)
            : await FailAsync(CreateDbContext(), f);

        result.Status().Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task TrialProgressOnADisabledGame_LeavesEveryOfficialScoreAndRankUntouched()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 100, allowTrialRuns: true, enabled: false);
        var rival = await Fixtures.AddUserAsync(db, "rival");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = rival.Id });
        // Pinned so "their ranks still match" is a statement about the trial
        // rather than about whichever tie-break mode happens to be default.
        (await db.Events.SingleAsync(e => e.Id == f.Event.Id)).TieBreakMode = TieBreakMode.SharedPlace;
        await db.SaveChangesAsync();

        await EnableAsync(CreateDbContext(), f);
        await StartAsync(CreateDbContext(), f);
        await CompleteAsync(CreateDbContext(), f);

        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, CreateDbContext(), default);
        var entry = scoreboard!.Entries.Single(e => e.UserId == f.Competitor.Id);
        var rivalEntry = scoreboard.Entries.Single(e => e.UserId == rival.Id);

        // 100 points of practice on a game nobody has officially scored on
        // must not break the tie between the two competitors.
        entry.TotalScore.Should().Be(0);
        rivalEntry.TotalScore.Should().Be(0);
        entry.Rank.Should().Be(rivalEntry.Rank);

        var game = entry.Games.Single(g => g.EventGameId == f.Game.Id);
        game.IsEnabled.Should().BeFalse();
        game.Trial!.Score.Should().Be(100);
    }

    [Fact]
    public async Task MyTrialRuns_ReportsTheCallersOwnRunWithItsProgress()
    {
        var f = await RecordingTrialAsync(score: 20);
        await CompleteAsync(CreateDbContext(), f);

        var result = await MyTrialRunsEndpoint.List(f.Competitor.Principal(), CreateDbContext(), default);

        var run = result.Value<List<MyTrialRunResponse>>().Single();
        run.EventId.Should().Be(f.Event.Id);
        run.EventGameId.Should().Be(f.Game.Id);
        run.CompetitorId.Should().Be(f.Competitor.Id);
        run.IsOwnTrial.Should().BeTrue();
        run.State.Should().Be(nameof(TrialRunState.Running));
        run.Score.Should().Be(20);
        run.CompletedCount.Should().Be(1);
        run.TotalObjectives.Should().Be(1);
    }

    [Fact]
    public async Task MyTrialRuns_IncludesANotStartedRunSoStartIsReachable()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        await EnableAsync(db, f);

        var result = await MyTrialRunsEndpoint.List(f.Competitor.Principal(), CreateDbContext(), default);

        // Unlike the scoreboard, which reports nothing until a run starts, the
        // management tab has to show it or Start is unreachable.
        var run = result.Value<List<MyTrialRunResponse>>().Single();
        run.State.Should().Be(nameof(TrialRunState.NotStarted));
        run.Score.Should().Be(0);
    }

    [Fact]
    public async Task MyTrialRuns_DoesNotLeakAFellowCompetitorsRun()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = stranger.Id });
        await db.SaveChangesAsync();
        await Fixtures.AddTrialRunAsync(CreateDbContext(), f);

        // A fellow competitor is not an owner, a delegate, or the subject.
        var result = await MyTrialRunsEndpoint.List(stranger.Principal(), CreateDbContext(), default);

        result.Value<List<MyTrialRunResponse>>().Should().BeEmpty();
    }

    [Fact]
    public async Task MyTrialRunObjectives_ReportTheRunsOwnRowsAndRefuseAStranger()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 5, allowTrialRuns: true);
        var pending = await Fixtures.AddObjectiveAsync(db, f.Game, "Pending", 5);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = stranger.Id });
        await db.SaveChangesAsync();

        var runId = (await EnableAsync(CreateDbContext(), f)).Value<TrialRunResponse>().Id;
        await StartAsync(CreateDbContext(), f);
        await CompleteAsync(CreateDbContext(), f);

        var mine = await MyTrialRunsEndpoint.GetObjectives(
            runId, f.Competitor.Principal(), CreateDbContext(), default);

        var game = mine.Value<MyEventObjectivesResponse>().Games.Single();
        game.GameId.Should().Be(f.Game.Id);
        game.Objectives.Single(o => o.ObjectiveId == f.Objective.Id).Completed.Should().BeTrue();
        game.Objectives.Single(o => o.ObjectiveId == pending.Id).Completed.Should().BeFalse();

        var refused = await MyTrialRunsEndpoint.GetObjectives(
            runId, stranger.Principal(), CreateDbContext(), default);

        refused.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MyTrialRunObjectives_ForAnUnknownRun_IsNotFound()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db, "user");

        var result = await MyTrialRunsEndpoint.GetObjectives(
            Guid.NewGuid(), user.Principal(), db, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Complete_WithAMatchingExpectedTrialRunId_Succeeds()
    {
        var f = await RecordingTrialAsync();
        var runId = (await CreateDbContext().TrialRuns
            .SingleAsync(t => t.EventGameId == f.Game.Id && t.UserId == f.Competitor.Id)).Id;

        var result = await CompleteAsync(CreateDbContext(), f, expectedTrialRunId: runId);

        result.Status().Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task Complete_WithAStaleExpectedTrialRunId_Conflicts()
    {
        var f = await RecordingTrialAsync();

        // The client believed it was ticking against a different run — most
        // likely one that has since been reset and replaced.
        var result = await CompleteAsync(CreateDbContext(), f, expectedTrialRunId: Guid.NewGuid());

        result.Status().Should().Be(StatusCodes.Status409Conflict);
        (await CreateDbContext().CompletedObjectives.CountAsync(c => c.ObjectiveId == f.Objective.Id))
            .Should().Be(0);
    }

    /// <summary>An event with trials allowed and this competitor's run recording.</summary>
    private async Task<EventFixture> RecordingTrialAsync(int score = 10)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: score, allowTrialRuns: true);
        await EnableAsync(db, f);
        await StartAsync(CreateDbContext(), f);
        return f;
    }

    private Task<IResult> EnableAsync(AppDbContext db, EventFixture f) =>
        TrialRunsEndpoint.EnableTrialRun(
            f.Event.Id, f.Game.Id, f.Competitor.Id, f.Competitor.Principal(), db, Audit, Cache, default);

    private Task<IResult> DisableAsync(AppDbContext db, EventFixture f) =>
        TrialRunsEndpoint.DisableTrialRun(
            f.Event.Id, f.Game.Id, f.Competitor.Id, f.Competitor.Principal(), db, Audit, Cache, default);

    private Task<IResult> StartAsync(AppDbContext db, EventFixture f) =>
        TrialRunsEndpoint.StartTrialRun(
            f.Event.Id, f.Game.Id, f.Competitor.Id, f.Competitor.Principal(), db, Audit, Cache, default);

    private Task<IResult> StopAsync(AppDbContext db, EventFixture f) =>
        TrialRunsEndpoint.StopTrialRun(
            f.Event.Id, f.Game.Id, f.Competitor.Id, f.Competitor.Principal(), db, Audit, Cache, default);

    private Task<IResult> ResetAsync(AppDbContext db, EventFixture f) =>
        TrialRunsEndpoint.ResetTrialRun(
            f.Event.Id, f.Game.Id, f.Competitor.Id, f.Competitor.Principal(), db, Audit, Cache, default);

    private Task<IResult> CompleteAsync(AppDbContext db, EventFixture f, Guid? expectedTrialRunId = null) =>
        CompletedObjectivesEndpoint.CompleteObjective(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Principal(),
            db, Audit, Cache, NullLogger<CompletedObjectivesEndpoint>.Instance, default,
            expectedTrialRunId: expectedTrialRunId);

    private Task<IResult> FailAsync(AppDbContext db, EventFixture f) =>
        FailedObjectivesEndpoint.FailObjective(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Principal(),
            db, Audit, Cache, NullLogger<FailedObjectivesEndpoint>.Instance, default);

    private async Task<ScoreboardEntry> OfficialEntryAsync(EventFixture f)
    {
        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, CreateDbContext(), default);
        return scoreboard!.Entries.Single(e => e.UserId == f.Competitor.Id);
    }

    private async Task AssertOfficialAsync(EventFixture f, int expectedScore, int expectedCompleted)
    {
        var entry = await OfficialEntryAsync(f);
        entry.TotalScore.Should().Be(expectedScore);
        entry.CompletedCount.Should().Be(expectedCompleted);
    }
}
