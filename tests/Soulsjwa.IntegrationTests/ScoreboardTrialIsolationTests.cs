using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The scoreboard reports trial progress next to the official figures, so the
/// aggregation must keep the two apart: nothing recorded inside a trial run may
/// reach an official total, a competitor's standing, or their rank. This covers
/// the query and aggregation logic, where the two could be conflated; the
/// partial unique indexes keeping the rows distinct are
/// <see cref="PartialUniqueIndexTests"/>'s subject.
/// </summary>
public class ScoreboardTrialIsolationTests : IntegrationTestBase
{
    [Fact]
    public async Task TrialCompletions_StayOutOfEveryOfficialTotal()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var objective = await AddObjectiveAsync(db, game, score: 40);
        var trialRun = await AddTrialRunAsync(db, ev, game, competitor, TrialRunState.Running);

        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = trialRun.Id,
        });
        await db.SaveChangesAsync();

        var entry = await SingleEntryAsync(db, ev.Id, competitor.Id);

        entry.TotalScore.Should().Be(0);
        entry.CompletedCount.Should().Be(0);
        entry.FailedCount.Should().Be(0);
        entry.IsFinished.Should().BeFalse();
        entry.Status.Should().Be(nameof(ObjectiveOutcome.Pending));
        entry.LastCompletedAt.Should().BeNull();

        var breakdown = entry.Games.Single();
        breakdown.Score.Should().Be(0);
        breakdown.CompletedCount.Should().Be(0);
        breakdown.Objectives.Single().IsCompleted.Should().BeFalse();

        // ...and is reported on its own terms instead.
        breakdown.Trial!.Score.Should().Be(40);
        breakdown.Trial.CompletedCount.Should().Be(1);
        breakdown.Objectives.Single().Trial!.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task TrialCompletions_DoNotDisturbRanking()
    {
        var db = CreateDbContext();
        var (ev, game, quiet) = await SeedAsync(db);
        var trialing = new User { DisplayName = "Trialing", TwitchLogin = "trialing", TwitchId = "t2" };
        db.Users.Add(trialing);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = trialing.Id });
        await db.SaveChangesAsync();

        var objective = await AddObjectiveAsync(db, game, score: 50);
        var trialRun = await AddTrialRunAsync(db, ev, game, trialing, TrialRunState.Running);

        // Both competitors sit on the same official score. Fixed timestamps
        // keep the tie-break deterministic so the comparison below is only
        // ever about the trial rows.
        db.CompletedObjectives.AddRange(
            new CompletedObjective
            {
                ObjectiveId = objective.Id,
                UserId = quiet.Id,
                CompletedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            },
            new CompletedObjective
            {
                ObjectiveId = objective.Id,
                UserId = trialing.Id,
                CompletedAt = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            });
        await db.SaveChangesAsync();

        var before = await ScoreboardEndpoint.BuildAsync(ev.Id, db, CancellationToken.None);
        var baseline = before!.Entries
            .Select(e => (e.UserId, e.TotalScore, e.Rank))
            .ToList();

        // Now pile up practice progress worth far more than anyone's real score.
        var extra = await AddObjectiveAsync(db, game, name: "Extra", score: 9999);
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = extra.Id,
            UserId = trialing.Id,
            TrialRunId = trialRun.Id,
            CompletedAt = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();

        var after = await ScoreboardEndpoint.BuildAsync(ev.Id, db, CancellationToken.None);

        // Same order, same ranks, same scores — whatever the event's tie-break
        // mode, 9999 points of practice moved nothing.
        after!.Entries.Select(e => (e.UserId, e.TotalScore, e.Rank)).Should().Equal(baseline);
        after.Entries.Single(e => e.UserId == trialing.Id)
            .Games.Single(g => g.Trial is not null).Trial!.Score.Should().Be(9999);
    }

    [Fact]
    public async Task TrialFailures_StayOutOfTheOfficialFailedCount()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var objective = await AddObjectiveAsync(db, game);
        var trialRun = await AddTrialRunAsync(db, ev, game, competitor, TrialRunState.Running);

        db.FailedObjectives.Add(new FailedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = trialRun.Id,
        });
        await db.SaveChangesAsync();

        var entry = await SingleEntryAsync(db, ev.Id, competitor.Id);

        entry.FailedCount.Should().Be(0);
        entry.Status.Should().Be(nameof(ObjectiveOutcome.Pending));
        var breakdown = entry.Games.Single();
        breakdown.FailedCount.Should().Be(0);
        breakdown.Objectives.Single().IsFailed.Should().BeFalse();
        breakdown.Trial!.FailedCount.Should().Be(1);
        breakdown.Objectives.Single().Trial!.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task OfficialAndTrialCompletionsOfTheSameObjective_AreReportedSeparately()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var objective = await AddObjectiveAsync(db, game, score: 25);
        var trialRun = await AddTrialRunAsync(db, ev, game, competitor, TrialRunState.Running);

        db.CompletedObjectives.AddRange(
            new CompletedObjective { ObjectiveId = objective.Id, UserId = competitor.Id },
            new CompletedObjective
            {
                ObjectiveId = objective.Id,
                UserId = competitor.Id,
                TrialRunId = trialRun.Id,
            });
        await db.SaveChangesAsync();

        var entry = await SingleEntryAsync(db, ev.Id, competitor.Id);

        // The official completion is counted exactly once — a trial row for the
        // same objective must not double it.
        entry.TotalScore.Should().Be(25);
        entry.CompletedCount.Should().Be(1);
        var breakdown = entry.Games.Single();
        breakdown.Objectives.Single().IsCompleted.Should().BeTrue();
        breakdown.Trial!.Score.Should().Be(25);
        breakdown.Trial.CompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task TrialProgress_IsNotReportedForANotStartedRun()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        await AddObjectiveAsync(db, game);
        await AddTrialRunAsync(db, ev, game, competitor, TrialRunState.NotStarted);

        var breakdown = (await SingleEntryAsync(db, ev.Id, competitor.Id)).Games.Single();

        breakdown.Trial.Should().BeNull();
        breakdown.IsTrialActive.Should().BeFalse();

        // ...but the slot itself is still reported, because its existence is
        // what refuses official writes on this game. This is the flag the UI
        // gates its checkboxes on, and it is the only trial signal a
        // NotStarted run produces.
        breakdown.HasTrialRun.Should().BeTrue();
    }

    [Fact]
    public async Task HasTrialRun_IsFalseWithNoSlot_AndTrueForEveryStateThatHasOne()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        await AddObjectiveAsync(db, game);

        var before = (await SingleEntryAsync(db, ev.Id, competitor.Id)).Games.Single();
        before.HasTrialRun.Should().BeFalse();

        foreach (var state in new[]
                 {
                     TrialRunState.NotStarted, TrialRunState.Running,
                     TrialRunState.Paused, TrialRunState.Completed,
                 })
        {
            var run = await db.TrialRuns.FindAsync(
                (await AddOrUpdateTrialRunAsync(db, ev, game, competitor, state)).Id);
            run!.State.Should().Be(state);

            var breakdown = (await SingleEntryAsync(db, ev.Id, competitor.Id)).Games.Single();
            breakdown.HasTrialRun.Should().BeTrue($"a {state} slot still blocks official writes");
        }
    }

    [Fact]
    public async Task PausedTrial_KeepsReportingItsScoreButIsNotActive()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var objective = await AddObjectiveAsync(db, game, score: 15);
        var trialRun = await AddTrialRunAsync(db, ev, game, competitor, TrialRunState.Paused);

        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = trialRun.Id,
        });
        await db.SaveChangesAsync();

        var breakdown = (await SingleEntryAsync(db, ev.Id, competitor.Id)).Games.Single();

        breakdown.IsTrialActive.Should().BeFalse();
        breakdown.Trial!.State.Should().Be(nameof(TrialRunState.Paused));
        breakdown.Trial.Score.Should().Be(15);
    }

    [Fact]
    public async Task AnotherCompetitorsTrialRun_NeverAppearsOnThisCompetitor()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var other = new User { DisplayName = "Other", TwitchLogin = "other", TwitchId = "t3" };
        db.Users.Add(other);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = other.Id });
        await db.SaveChangesAsync();

        var objective = await AddObjectiveAsync(db, game, score: 30);
        var otherTrial = await AddTrialRunAsync(db, ev, game, other, TrialRunState.Running);
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = other.Id,
            TrialRunId = otherTrial.Id,
        });
        await db.SaveChangesAsync();

        var response = await ScoreboardEndpoint.BuildAsync(ev.Id, db, CancellationToken.None);

        var mine = response!.Entries.Single(e => e.UserId == competitor.Id).Games.Single();
        mine.Trial.Should().BeNull();
        mine.Objectives.Single().Trial.Should().BeNull();

        var theirs = response.Entries.Single(e => e.UserId == other.Id).Games.Single();
        theirs.Trial!.Score.Should().Be(30);
    }

    [Fact]
    public async Task BuildSummaryBatch_IgnoresTrialProgressEntirely()
    {
        var db = CreateDbContext();
        var (ev, game, competitor) = await SeedAsync(db);
        var objective = await AddObjectiveAsync(db, game, score: 70);
        var trialRun = await AddTrialRunAsync(db, ev, game, competitor, TrialRunState.Running);

        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = trialRun.Id,
        });
        await db.SaveChangesAsync();

        var summaries = await ScoreboardEndpoint.BuildSummaryBatchAsync(
            [ev.Id], db, CancellationToken.None);

        var summary = summaries[ev.Id].Single(s => s.UserId == competitor.Id);
        summary.TotalScore.Should().Be(0);
        summary.CompletedCount.Should().Be(0);
        summary.LastCompletedAt.Should().BeNull();
    }

    private static async Task<ScoreboardEntry> SingleEntryAsync(
        AppDbContext db, Guid eventId, Guid userId)
    {
        var response = await ScoreboardEndpoint.BuildAsync(eventId, db, CancellationToken.None);
        return response!.Entries.Single(e => e.UserId == userId);
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

    /// <summary>
    /// Moves the competitor's single slot for this game to <paramref name="state"/>,
    /// creating it the first time. The unique (EventGameId, UserId) index means
    /// a state sweep has to reuse the row rather than add one per state.
    /// </summary>
    private static async Task<TrialRun> AddOrUpdateTrialRunAsync(
        AppDbContext db, Event ev, EventGame game, User user, TrialRunState state)
    {
        var existing = db.TrialRuns.Local
            .FirstOrDefault(t => t.EventGameId == game.Id && t.UserId == user.Id);
        if (existing is null) return await AddTrialRunAsync(db, ev, game, user, state);

        existing.State = state;
        existing.StartedAt = state == TrialRunState.NotStarted ? null : DateTime.UtcNow;
        await db.SaveChangesAsync();
        return existing;
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
