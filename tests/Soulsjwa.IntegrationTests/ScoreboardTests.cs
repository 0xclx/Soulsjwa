using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The per-game breakdown, the terminal status once every objective is
/// resolved, and the two tie-break modes — computed over rows read from the
/// database, so these call the builder directly.
/// <see cref="ScoreboardTrialIsolationTests"/> covers the same builder's
/// official-versus-trial split; the wire contract is
/// <c>Soulsjwa.ApiTests.ScoreboardEndpointTests</c>.
/// </summary>
public class ScoreboardTests : IntegrationTestBase
{
    [Fact]
    public async Task AnEventWithNoCompetitors_HasAnEmptyScoreboard()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = new Event { Name = "empty", CreatedById = owner.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var scoreboard = await ScoreboardEndpoint.BuildAsync(ev.Id, CreateDbContext(), default);

        scoreboard!.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEventThatDoesNotExist_HasNoScoreboardAtAll()
    {
        var scoreboard = await ScoreboardEndpoint.BuildAsync(Guid.NewGuid(), CreateDbContext(), default);

        scoreboard.Should().BeNull("which is what the endpoint turns into a 404");
    }

    [Fact]
    public async Task ACompletion_ShowsUpInTheTotalAndThePerGameBreakdown()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 15);
        db.Objectives.Attach(f.Objective).Entity.Category = "Undead Parish";
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = f.Objective.Id,
            UserId = f.Competitor.Id,
        });
        await db.SaveChangesAsync();

        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, CreateDbContext(), default);

        var entry = scoreboard!.Entries.Should().ContainSingle().Subject;
        entry.UserId.Should().Be(f.Competitor.Id);
        entry.TotalScore.Should().Be(15);
        entry.CompletedCount.Should().Be(1);
        entry.TwitchLogin.Should().NotBeNullOrEmpty();

        var game = entry.Games.Should().ContainSingle().Subject;
        game.Score.Should().Be(15);
        var objective = game.Objectives.Should().ContainSingle().Subject;
        objective.IsCompleted.Should().BeTrue();
        objective.Category.Should().Be("Undead Parish", "the category is what the UI groups by");
    }

    [Fact]
    public async Task ACompetitorWhoseLastObjectiveFailed_IsFinishedAndFailed()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        db.FailedObjectives.Add(new FailedObjective
        {
            ObjectiveId = f.Objective.Id,
            UserId = f.Competitor.Id,
        });
        await db.SaveChangesAsync();

        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, CreateDbContext(), default);

        var entry = scoreboard!.Entries.Should().ContainSingle().Subject;
        entry.IsFinished.Should().BeTrue("every objective is resolved, so there is nothing left to play for");
        entry.FailedCount.Should().Be(1);
        entry.Status.Should().Be(nameof(ObjectiveOutcome.Failed));
    }

    [Fact]
    public async Task SetLive_ByACompetitor_ShowsOnTheirScoreboardRow()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await ScoreboardEndpoint.SetLive(
            f.Event.Id, new SetLiveRequest(true), f.Competitor.Principal(), db, Audit, Cache, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, CreateDbContext(), default);
        scoreboard!.Entries.Should().ContainSingle(e => e.UserId == f.Competitor.Id && e.IsLive);
    }

    [Fact]
    public async Task SetLive_BySomeoneWhoIsNotACompetitor_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await ScoreboardEndpoint.SetLive(
            f.Event.Id, new SetLiveRequest(true), stranger.Principal(), db, Audit, Cache, default);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ByTime_SplitsATieOnInGameTimeRatherThanWallClock()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, withCompetitor: false);
        var a = await AddCompetitorAsync(db, f.Event, "a");
        var b = await AddCompetitorAsync(db, f.Event, "b");
        db.Events.Attach(f.Event).Entity.TieBreakMode = TieBreakMode.ByTime;

        // a finished later in real time but faster in game; b the reverse.
        // In-game time is the tie-break, so a must come first.
        var earlier = DateTime.UtcNow.AddHours(-2);
        db.CompletedObjectives.AddRange(
            new CompletedObjective
            {
                ObjectiveId = f.Objective.Id,
                UserId = a.Id,
                CompletedAt = earlier.AddHours(1),
                InGameTimeMs = 5000,
            },
            new CompletedObjective
            {
                ObjectiveId = f.Objective.Id,
                UserId = b.Id,
                CompletedAt = earlier,
                InGameTimeMs = 9000,
            });
        await db.SaveChangesAsync();

        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, CreateDbContext(), default);

        scoreboard!.TieBreakMode.Should().Be(nameof(TieBreakMode.ByTime));
        scoreboard.Entries.Should().HaveCount(2);
        scoreboard.Entries[0].UserId.Should().Be(a.Id, "a has the lower in-game time");
        scoreboard.Entries[0].Rank.Should().Be(1);
        scoreboard.Entries[0].TotalInGameTimeMs.Should().Be(5000);
        scoreboard.Entries[1].UserId.Should().Be(b.Id);
        scoreboard.Entries[1].Rank.Should().Be(2);
        scoreboard.Entries[1].TotalInGameTimeMs.Should().Be(9000);
    }

    [Fact]
    public async Task SharedPlace_GivesEveryTiedCompetitorTheSameRank()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, withCompetitor: false);
        var a = await AddCompetitorAsync(db, f.Event, "a");
        var b = await AddCompetitorAsync(db, f.Event, "b");
        var c = await AddCompetitorAsync(db, f.Event, "c");
        db.Events.Attach(f.Event).Entity.TieBreakMode = TieBreakMode.SharedPlace;

        // Equal scores, deliberately unequal times: in this mode neither
        // wall-clock nor in-game time may separate them.
        var when = DateTime.UtcNow.AddHours(-1);
        db.CompletedObjectives.AddRange(
            new CompletedObjective { ObjectiveId = f.Objective.Id, UserId = a.Id, CompletedAt = when, InGameTimeMs = 1000 },
            new CompletedObjective { ObjectiveId = f.Objective.Id, UserId = b.Id, CompletedAt = when.AddMinutes(5), InGameTimeMs = 1000 },
            new CompletedObjective { ObjectiveId = f.Objective.Id, UserId = c.Id, CompletedAt = when, InGameTimeMs = 5000 });
        await db.SaveChangesAsync();

        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, CreateDbContext(), default);

        scoreboard!.TieBreakMode.Should().Be(nameof(TieBreakMode.SharedPlace));
        scoreboard.Entries.Select(e => e.Rank).Should().AllBeEquivalentTo(1);
    }

    private static async Task<User> AddCompetitorAsync(AppDbContext db, Event ev, string prefix)
    {
        var user = await Fixtures.AddUserAsync(db, prefix);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = user.Id });
        await db.SaveChangesAsync();
        return user;
    }
}
