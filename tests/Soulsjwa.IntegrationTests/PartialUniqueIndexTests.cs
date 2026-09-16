using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The partial unique indexes that make "one official row, one row per trial
/// run" a fact about the data rather than a promise the handlers keep. They are
/// partial because Postgres treats NULLs as distinct: a plain unique index on
/// <c>(ObjectiveId, UserId, TrialRunId)</c> would accept a second official
/// completion, since <c>NULL &lt;&gt; NULL</c>. Needs real Postgres — the
/// InMemory provider enforces no index at all. Two contexts throughout, not
/// one: a duplicate caught by EF's identity map would prove nothing.
/// </summary>
public class PartialUniqueIndexTests : IntegrationTestBase
{
    [Fact]
    public async Task ASecondFeaturedEvent_IsRejected()
    {
        var db = CreateDbContext();
        var user = await AddUserAsync(db);
        db.Events.Add(new Event { Name = "First", CreatedById = user.Id, IsFeatured = true });
        await db.SaveChangesAsync();

        var second = CreateDbContext();
        second.Events.Add(new Event { Name = "Second", CreatedById = user.Id, IsFeatured = true });

        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "IX_Events_FeaturedEvent is unique WHERE \"IsFeatured\"");
    }

    [Fact]
    public async Task ManyUnfeaturedEvents_AreFine()
    {
        var db = CreateDbContext();
        var user = await AddUserAsync(db);
        db.Events.Add(new Event { Name = "A", CreatedById = user.Id });
        db.Events.Add(new Event { Name = "B", CreatedById = user.Id });
        db.Events.Add(new Event { Name = "C", CreatedById = user.Id });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "the index is filtered, so it says nothing about unfeatured rows");
    }

    [Fact]
    public async Task ASecondOfficialCompletionOfTheSameObjective_IsRejected()
    {
        var db = CreateDbContext();
        var (_, _, competitor, objective) = await SeedAsync(db);
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = objective.Id, UserId = competitor.Id });
        await db.SaveChangesAsync();

        var second = CreateDbContext();
        second.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = objective.Id, UserId = competitor.Id });

        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "two official completions of one objective would double-count its score");
    }

    [Fact]
    public async Task ASecondCompletionWithinTheSameTrialRun_IsRejected()
    {
        var db = CreateDbContext();
        var (ev, game, competitor, objective) = await SeedAsync(db);
        var run = await AddTrialRunAsync(db, ev, game, competitor);
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = run.Id,
        });
        await db.SaveChangesAsync();

        var second = CreateDbContext();
        second.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = run.Id,
        });

        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task TheSameObjectiveCompletedInTwoDifferentTrialRuns_IsFine()
    {
        var db = CreateDbContext();
        var (ev, game, competitor, objective) = await SeedAsync(db);
        var run = await AddTrialRunAsync(db, ev, game, competitor);

        // A second game means a second slot: the unique (EventGameId, UserId)
        // index below forbids two runs on the same one.
        var otherGame = new EventGame { EventId = ev.Id, CustomGameName = "Other" };
        db.EventGames.Add(otherGame);
        await db.SaveChangesAsync();
        var otherRun = await AddTrialRunAsync(db, ev, otherGame, competitor);

        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = run.Id,
        });
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = otherRun.Id,
        });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "each run keeps its own progress — the trial index includes TrialRunId");
    }

    [Fact]
    public async Task ASecondOfficialFailureOfTheSameObjective_IsRejected()
    {
        var db = CreateDbContext();
        var (_, _, competitor, objective) = await SeedAsync(db);
        db.FailedObjectives.Add(new FailedObjective { ObjectiveId = objective.Id, UserId = competitor.Id });
        await db.SaveChangesAsync();

        var second = CreateDbContext();
        second.FailedObjectives.Add(new FailedObjective { ObjectiveId = objective.Id, UserId = competitor.Id });

        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ASecondFailureWithinTheSameTrialRun_IsRejected()
    {
        var db = CreateDbContext();
        var (ev, game, competitor, objective) = await SeedAsync(db);
        var run = await AddTrialRunAsync(db, ev, game, competitor);
        db.FailedObjectives.Add(new FailedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = run.Id,
        });
        await db.SaveChangesAsync();

        var second = CreateDbContext();
        second.FailedObjectives.Add(new FailedObjective
        {
            ObjectiveId = objective.Id,
            UserId = competitor.Id,
            TrialRunId = run.Id,
        });

        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ASecondTrialRunForTheSameCompetitorAndGame_IsRejected()
    {
        var db = CreateDbContext();
        var (ev, game, competitor, _) = await SeedAsync(db);
        await AddTrialRunAsync(db, ev, game, competitor);

        var second = CreateDbContext();
        second.TrialRuns.Add(new TrialRun
        {
            EventId = ev.Id,
            EventGameId = game.Id,
            UserId = competitor.Id,
            State = TrialRunState.NotStarted,
        });

        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "enabling trial mode twice must reuse the one slot, not add a second");
    }

    private static async Task<User> AddUserAsync(AppDbContext db)
    {
        var user = new User
        {
            TwitchId = Guid.NewGuid().ToString(),
            TwitchLogin = "owner-" + Guid.NewGuid().ToString("N")[..8],
            DisplayName = "Owner",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(Event Event, EventGame Game, User Competitor, Objective Objective)> SeedAsync(AppDbContext db)
    {
        var user = await AddUserAsync(db);
        var ev = new Event { Name = "Event", CreatedById = user.Id, IsStarted = true };
        var game = new EventGame { CustomGameName = "Custom Game", IsEnabled = true };
        ev.EventGames.Add(game);
        db.Events.Add(ev);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = user.Id });
        await db.SaveChangesAsync();

        var objective = new Objective { EventGameId = game.Id, Name = "Objective", Score = 10 };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();

        return (ev, game, user, objective);
    }

    private static async Task<TrialRun> AddTrialRunAsync(AppDbContext db, Event ev, EventGame game, User user)
    {
        var run = new TrialRun
        {
            EventId = ev.Id,
            EventGameId = game.Id,
            UserId = user.Id,
            State = TrialRunState.Running,
            StartedAt = DateTime.UtcNow,
        };
        db.TrialRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }
}
