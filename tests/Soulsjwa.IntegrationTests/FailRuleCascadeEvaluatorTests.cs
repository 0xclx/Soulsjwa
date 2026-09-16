using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Entities;
using Soulsjwa.Api.Features.Games.Services;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

public class FailRuleCascadeEvaluatorTests : IntegrationTestBase
{
    private static async Task<(Event Event, EventGame EventGame, User Completer, User CandidateA, User CandidateB)> SeedAsync(AppDbContext db)
    {
        var completer = new User { TwitchId = "1", TwitchLogin = "completer", DisplayName = "Completer" };
        var candidateA = new User { TwitchId = "2", TwitchLogin = "candidatea", DisplayName = "CandidateA" };
        var candidateB = new User { TwitchId = "3", TwitchLogin = "candidateb", DisplayName = "CandidateB" };
        db.Users.AddRange(completer, candidateA, candidateB);

        // Not added: the Games catalogue is seeded by the migration, so
        // inserting an Id of our own would collide with a real row.
        var game = await db.Games.OrderBy(g => g.Id).FirstAsync();

        var ev = new Event { Name = "Test Event", CreatedById = completer.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var eventGame = new EventGame { EventId = ev.Id, KnownGameId = game.Id };
        db.EventGames.Add(eventGame);
        await db.SaveChangesAsync();

        db.EventCompetitors.AddRange(
            new EventCompetitor { EventId = ev.Id, UserId = completer.Id },
            new EventCompetitor { EventId = ev.Id, UserId = candidateA.Id },
            new EventCompetitor { EventId = ev.Id, UserId = candidateB.Id });
        await db.SaveChangesAsync();

        return (ev, eventGame, completer, candidateA, candidateB);
    }

    [Fact]
    public async Task ApplyAsync_BatchOfTwoObjectives_CascadesIndependentlyForEach()
    {
        var db = CreateDbContext();
        var (_, eventGame, completer, candidateA, candidateB) = await SeedAsync(db);

        var obj1 = new Objective
        {
            EventGameId = eventGame.Id,
            Name = "Obj1",
            Score = 1,
            FailRule = """{">=":[{"var":"competitorCompletions"},1]}""",
        };
        var obj2 = new Objective
        {
            EventGameId = eventGame.Id,
            Name = "Obj2",
            Score = 1,
            FailRule = """{">=":[{"var":"competitorCompletions"},1]}""",
        };
        db.Objectives.AddRange(obj1, obj2);
        await db.SaveChangesAsync();

        // The completer just completed both objectives officially.
        db.CompletedObjectives.AddRange(
            new CompletedObjective { ObjectiveId = obj1.Id, UserId = completer.Id },
            new CompletedObjective { ObjectiveId = obj2.Id, UserId = completer.Id });
        await db.SaveChangesAsync();

        var newlyFailed = await FailRuleCascadeEvaluator.ApplyAsync(
            db, [obj1.Id, obj2.Id], completer.Id, CancellationToken.None);

        // 2 other pending competitors x 2 objectives = 4 new failures.
        newlyFailed.Should().Be(4);
        var failedRows = await db.FailedObjectives.ToListAsync();
        failedRows.Should().HaveCount(4);
        foreach (var uid in new[] { candidateA.Id, candidateB.Id })
        {
            failedRows.Should().Contain(f => f.UserId == uid && f.ObjectiveId == obj1.Id);
            failedRows.Should().Contain(f => f.UserId == uid && f.ObjectiveId == obj2.Id);
        }
        failedRows.Should().NotContain(f => f.UserId == completer.Id);
    }

    [Fact]
    public async Task ApplyAsync_FailRuleNotCountOnly_NeverCascades()
    {
        var db = CreateDbContext();
        var (_, eventGame, completer, _, _) = await SeedAsync(db);

        var objective = new Objective
        {
            EventGameId = eventGame.Id,
            Name = "ReadsGameState",
            Score = 1,
            // References a game-state variable, not just competitorCompletions —
            // not "count-only", so the cascade must never resolve it without a
            // fresh submission from the candidate.
            FailRule = """{"and":[{">=":[{"var":"competitorCompletions"},1]},{">":[{"var":"deaths"},0]}]}""",
        };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = objective.Id, UserId = completer.Id });
        await db.SaveChangesAsync();

        var newlyFailed = await FailRuleCascadeEvaluator.ApplyAsync(
            db, [objective.Id], completer.Id, CancellationToken.None);

        newlyFailed.Should().Be(0);
        (await db.FailedObjectives.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApplyAsync_TrialRows_AreExcludedFromCompletedAndFailedSets()
    {
        var db = CreateDbContext();
        var (_, eventGame, completer, candidateA, candidateB) = await SeedAsync(db);

        var objective = new Objective
        {
            EventGameId = eventGame.Id,
            Name = "Obj1",
            Score = 1,
            FailRule = """{">=":[{"var":"competitorCompletions"},1]}""",
        };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();

        var trialRun = new TrialRun { EventId = eventGame.EventId, EventGameId = eventGame.Id, UserId = candidateA.Id };
        db.TrialRuns.Add(trialRun);
        await db.SaveChangesAsync();

        // candidateA has only a TRIAL completion for this objective — must not
        // be treated as "already completed" (would wrongly skip it) and must
        // not itself count toward `competitorCompletions`.
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = candidateA.Id,
            TrialRunId = trialRun.Id,
        });
        // The official completion that triggers the cascade.
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = objective.Id, UserId = completer.Id });
        await db.SaveChangesAsync();

        var newlyFailed = await FailRuleCascadeEvaluator.ApplyAsync(
            db, [objective.Id], completer.Id, CancellationToken.None);

        // Both candidateA (still officially pending despite its trial
        // completion) and candidateB are failed.
        newlyFailed.Should().Be(2);
        var failedUserIds = await db.FailedObjectives.Select(f => f.UserId).ToListAsync();
        failedUserIds.Should().BeEquivalentTo([candidateA.Id, candidateB.Id]);
    }

    [Fact]
    public async Task ApplyAsync_CompletionsByNonCompetitors_DoNotCountTowardTheThreshold()
    {
        var db = CreateDbContext();
        var (ev, eventGame, completer, candidateA, candidateB) = await SeedAsync(db);

        // Someone who played and was then removed from the event. Removing a
        // competitor deletes only their EventCompetitor row, so their official
        // completion outlives their enrolment.
        var departed = new User { TwitchId = "4", TwitchLogin = "departed", DisplayName = "Departed" };
        db.Users.Add(departed);
        await db.SaveChangesAsync();

        var objective = new Objective
        {
            EventGameId = eventGame.Id,
            Name = "Obj1",
            Score = 1,
            // Needs TWO competitor completions to fail the rest.
            FailRule = """{">=":[{"var":"competitorCompletions"},2]}""",
        };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();

        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = objective.Id, UserId = departed.Id });
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = objective.Id, UserId = completer.Id });
        await db.SaveChangesAsync();

        var newlyFailed = await FailRuleCascadeEvaluator.ApplyAsync(
            db, [objective.Id], completer.Id, CancellationToken.None);

        // Only one *competitor* has completed it, so the threshold of two is
        // not met. Counting the departed user's row fired the rule here while
        // the connector's own count — which filters by enrolment — did not,
        // so which competitors got auto-failed depended on the code path.
        newlyFailed.Should().Be(0);
        (await db.FailedObjectives.AnyAsync()).Should().BeFalse();

        // Control: enrol them and the same call now does fail both candidates,
        // proving the rule itself is live rather than the fixture inert.
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = departed.Id });
        await db.SaveChangesAsync();

        var afterRejoin = await FailRuleCascadeEvaluator.ApplyAsync(
            db, [objective.Id], completer.Id, CancellationToken.None);

        afterRejoin.Should().Be(2);
        var failedUserIds = await db.FailedObjectives.Select(fo => fo.UserId).ToListAsync();
        failedUserIds.Should().BeEquivalentTo([candidateA.Id, candidateB.Id]);
    }

    [Fact]
    public async Task ApplyAsync_AlreadyResolvedCandidates_AreSkipped()
    {
        var db = CreateDbContext();
        var (_, eventGame, completer, candidateA, candidateB) = await SeedAsync(db);

        var objective = new Objective
        {
            EventGameId = eventGame.Id,
            Name = "Obj1",
            Score = 1,
            FailRule = """{">=":[{"var":"competitorCompletions"},1]}""",
        };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();

        // candidateA already completed it officially; candidateB already
        // failed it officially — neither is still "pending" and must not be
        // re-evaluated or duplicated.
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = objective.Id, UserId = candidateA.Id });
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = objective.Id, UserId = completer.Id });
        db.FailedObjectives.Add(new FailedObjective { ObjectiveId = objective.Id, UserId = candidateB.Id });
        await db.SaveChangesAsync();

        var newlyFailed = await FailRuleCascadeEvaluator.ApplyAsync(
            db, [objective.Id], completer.Id, CancellationToken.None);

        newlyFailed.Should().Be(0);
        (await db.FailedObjectives.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_EmptyObjectiveIdList_ReturnsZero()
    {
        var db = CreateDbContext();
        var newlyFailed = await FailRuleCascadeEvaluator.ApplyAsync(db, [], Guid.NewGuid(), CancellationToken.None);
        newlyFailed.Should().Be(0);
    }
}
