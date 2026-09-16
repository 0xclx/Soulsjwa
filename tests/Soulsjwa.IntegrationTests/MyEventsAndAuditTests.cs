using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Common.Models;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Endpoints;
using Soulsjwa.Api.Features.Audits.Entities;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Users.Endpoints;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// My Events, the user picker, and the audit log's two pagination modes — all
/// query shape rather than wire shape: what the aggregation counts, how many
/// SQL commands a page costs, and whether a cursor page pays for a
/// <c>COUNT</c>. The wire contract stays in <c>Soulsjwa.ApiTests</c>.
/// </summary>
public class MyEventsAndAuditTests : IntegrationTestBase
{
    [Fact]
    public async Task MyEvents_ReportsTheCallersProgressForAnEventTheyCompeteIn()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 40);
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = f.Objective.Id,
            UserId = f.Competitor.Id,
        });
        await db.SaveChangesAsync();

        var result = await MyEventsEndpoint.List(f.Competitor.Principal(), CreateDbContext(), default);

        var entry = result.Value<MyEventsResponse>().Competitor.Should().ContainSingle().Subject;
        entry.EventId.Should().Be(f.Event.Id);
        entry.Score.Should().Be(40);
        entry.TotalObjectives.Should().Be(1);
        entry.IncompleteObjectives.Should().Be(0);
    }

    /// <summary>
    /// "Stopped" used to be read back out of the audit log, which retention
    /// deletes — after which every finished event became "upcoming" again.
    /// The lifecycle now lives on the event row and survives the purge.
    /// </summary>
    [Fact]
    public async Task MyEvents_ReportsAStoppedEventAsStopped_EvenAfterItsAuditRowsAreGone()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: true, enabled: false);
        var stopped = await EventsEndpoint.StopEvent(
            f.Event.Id, f.Owner.Principal(), db, Audit, NullLogger<EventsEndpoint>.Instance, default);
        stopped.Status().Should().Be(StatusCodes.Status204NoContent);
        await db.AuditLogs.Where(a => a.EventId == f.Event.Id).ExecuteDeleteAsync();

        var result = await MyEventsEndpoint.List(f.Competitor.Principal(), CreateDbContext(), default);

        result.Value<MyEventsResponse>().Competitor.Should().ContainSingle().Which.Status.Should().Be("stopped");
    }

    [Fact]
    public async Task MyEvents_ReportsANeverStartedEventAsUpcoming_AndARestartedOneAsLive()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false, enabled: false);

        var upcoming = await MyEventsEndpoint.List(f.Competitor.Principal(), CreateDbContext(), default);
        upcoming.Value<MyEventsResponse>().Competitor.Should().ContainSingle().Which.Status.Should().Be("upcoming");

        await EventsEndpoint.StartEvent(f.Event.Id, f.Owner.Principal(), CreateDbContext(), Audit, NullLogger<EventsEndpoint>.Instance, default);
        await EventsEndpoint.StopEvent(f.Event.Id, f.Owner.Principal(), CreateDbContext(), Audit, NullLogger<EventsEndpoint>.Instance, default);
        await EventsEndpoint.StartEvent(f.Event.Id, f.Owner.Principal(), CreateDbContext(), Audit, NullLogger<EventsEndpoint>.Instance, default);

        var live = await MyEventsEndpoint.List(f.Competitor.Principal(), CreateDbContext(), default);
        live.Value<MyEventsResponse>().Competitor.Should().ContainSingle().Which.Status.Should().Be("live");
        var row = await CreateDbContext().Events.SingleAsync(e => e.Id == f.Event.Id);
        row.StartedAt.Should().NotBeNull();
        row.StoppedAt.Should().BeNull("a restart clears the stop so the next stop is the one that counts");
    }

    [Fact]
    public async Task MyEvents_GroupsAnEventByEveryRoleTheCallerHoldsInIt()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var streamer = await Fixtures.AddUserAsync(db, "streamer");
        db.EventCompetitors.Add(new EventCompetitor
        {
            EventId = f.Event.Id,
            UserId = streamer.Id,
            IsStreamer = true,
        });
        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = f.Event.Id,
            CompetitorUserId = streamer.Id,
            ModeratorUserId = f.Owner.Id,
        });
        await db.SaveChangesAsync();

        // The owner is also this streamer's delegated moderator, so the same
        // event has to appear under both headings.
        var result = await MyEventsEndpoint.List(f.Owner.Principal(), CreateDbContext(), default);

        var response = result.Value<MyEventsResponse>();
        response.Owned.Should().ContainSingle(e => e.EventId == f.Event.Id);
        response.Delegated.Should().ContainSingle(e => e.EventId == f.Event.Id);
    }

    [Fact]
    public async Task MyEvents_CountsOnlyTheEnabledGamesObjectivesAndScore()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 100, enabled: true);
        var disabledGame = new EventGame
        {
            EventId = f.Event.Id,
            CustomGameName = "Disabled Game",
            IsEnabled = false,
        };
        db.EventGames.Add(disabledGame);
        await db.SaveChangesAsync();
        var disabledObjective = await Fixtures.AddObjectiveAsync(db, disabledGame, "Disabled Objective", 200);
        db.CompletedObjectives.AddRange(
            new CompletedObjective { ObjectiveId = f.Objective.Id, UserId = f.Competitor.Id },
            new CompletedObjective { ObjectiveId = disabledObjective.Id, UserId = f.Competitor.Id });
        await db.SaveChangesAsync();

        var listed = await MyEventsEndpoint.List(f.Competitor.Principal(), CreateDbContext(), default);
        var objectives = await MyEventsEndpoint.GetObjectives(
            f.Event.Id, f.Competitor.Principal(), CreateDbContext(), default);

        var entry = listed.Value<MyEventsResponse>().Competitor.Should().ContainSingle().Subject;
        entry.TotalObjectives.Should().Be(1, "the disabled game is not being played");
        entry.Score.Should().Be(100);
        objectives.Value<MyEventObjectivesResponse>().Games
            .Should().ContainSingle().Which.GameId.Should().Be(f.Game.Id);
    }

    [Fact]
    public async Task MyEventObjectives_ForAnotherCompetitor_NeedADelegationAndAnId()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var streamer = await Fixtures.AddUserAsync(db, "streamer");
        var moderator = await Fixtures.AddUserAsync(db, "mod");
        db.EventCompetitors.Add(new EventCompetitor
        {
            EventId = f.Event.Id,
            UserId = streamer.Id,
            IsStreamer = true,
        });
        await db.SaveChangesAsync();

        // Without a delegation the moderator is nobody in this event.
        (await MyEventsEndpoint.GetObjectives(
                f.Event.Id, moderator.Principal(), CreateDbContext(), default, streamer.Id))
            .Status().Should().Be(StatusCodes.Status403Forbidden);

        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = f.Event.Id,
            CompetitorUserId = streamer.Id,
            ModeratorUserId = moderator.Id,
        });
        await db.SaveChangesAsync();

        (await MyEventsEndpoint.GetObjectives(
                f.Event.Id, moderator.Principal(), CreateDbContext(), default, streamer.Id))
            .Value<MyEventObjectivesResponse>()
            .CompetitorId.Should().Be(streamer.Id);
    }

    [Fact]
    public async Task MyEvents_RanksTheCallerTheSameWayTheScoreboardDoes()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 10, withCompetitor: false);
        (await db.Events.SingleAsync(e => e.Id == f.Event.Id)).TieBreakMode = TieBreakMode.SharedPlace;
        var tied = new List<User>();
        for (var i = 0; i < 3; i++)
        {
            var user = await Fixtures.AddUserAsync(db, $"tied{i}");
            db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = user.Id });
            db.CompletedObjectives.Add(new CompletedObjective
            {
                ObjectiveId = f.Objective.Id,
                UserId = user.Id,
                CompletedAt = DateTime.UtcNow.AddMinutes(-i),
            });
            tied.Add(user);
        }
        await db.SaveChangesAsync();

        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, CreateDbContext(), default);

        foreach (var user in tied)
        {
            var mine = await MyEventsEndpoint.List(user.Principal(), CreateDbContext(), default);
            var myRank = mine.Value<MyEventsResponse>().Competitor.Single().Rank;
            myRank.Should().Be(
                scoreboard!.Entries.Single(e => e.UserId == user.Id).Rank,
                "My Events and the scoreboard must not disagree about somebody's placing");
        }
    }

    [Fact]
    public async Task MyEvents_IssuesTheSameNumberOfQueriesWhateverTheEventCount()
    {
        var seed = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(seed, "owner");
        var competitor = await Fixtures.AddUserAsync(seed, "competitor");
        await AddCompetedEventsAsync(seed, owner, competitor, 2);

        var interceptor = new CommandCountingInterceptor();
        var db = CreateDbContext(interceptor);

        await MyEventsEndpoint.List(competitor.Principal(), db, default);
        var atTwoEvents = interceptor.Count;

        await AddCompetedEventsAsync(CreateDbContext(), owner, competitor, 8);
        interceptor.Reset();

        await MyEventsEndpoint.List(competitor.Principal(), db, default);

        interceptor.Count.Should().Be(atTwoEvents,
            "the number of SQL commands must not grow with the number of events");
    }

    [Fact]
    public async Task SearchUsers_WithTooShortAQuery_ReturnsNothingRatherThanEverything()
    {
        var db = CreateDbContext();
        await Fixtures.AddUserAsync(db, "searchable");

        var result = await SearchUsersEndpoint.Handle(db, default, "a");

        result.Value<List<UserSearchResultResponse>>().Should().BeEmpty();
    }

    [Fact]
    public async Task SearchUsers_MatchesASubstringOfTheLoginCaseInsensitively()
    {
        var db = CreateDbContext();
        var target = await Fixtures.AddUserAsync(db, "findme");
        await Fixtures.AddUserAsync(db, "someoneelse");

        var result = await SearchUsersEndpoint.Handle(db, default, "FINDME");

        result.Value<List<UserSearchResultResponse>>()
            .Should().ContainSingle(u => u.Id == target.Id);
    }

    [Fact]
    public async Task Audits_OffsetMode_CarriesATotalCount()
    {
        var db = CreateDbContext();
        var admin = await AddAuditRowsAsync(db, 3);

        var result = await AuditsEndpoint.ListForAdmin(admin.Principal(), CreateDbContext(), default, pageSize: 10);

        result.Value<PaginatedResponse<AuditLogResponse>>()
            .TotalCount.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Audits_OffsetMode_RefusesAPageBeyondTheCapButAllowsTheLastOne()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        (await AuditsEndpoint.ListForAdmin(admin.Principal(), CreateDbContext(), default, page: 100))
            .Status().Should().Be(StatusCodes.Status200OK);
        (await AuditsEndpoint.ListForAdmin(admin.Principal(), CreateDbContext(), default, page: 101))
            .Status().Should().Be(StatusCodes.Status400BadRequest,
                "a deep OFFSET is the cost this cap exists to avoid");
    }

    [Fact]
    public async Task Audits_CursorMode_OmitsTheTotalCountAndIssuesNoCountQuery()
    {
        var db = CreateDbContext();
        var admin = await AddAuditRowsAsync(db, 15);

        var first = await AuditsEndpoint.ListForAdmin(admin.Principal(), CreateDbContext(), default, pageSize: 10);
        var cursor = first.Value<PaginatedResponse<AuditLogResponse>>().NextCursor;
        cursor.Should().NotBeNullOrEmpty();

        var interceptor = new CommandCountingInterceptor();
        var counted = CreateDbContext(interceptor);
        var second = await AuditsEndpoint.ListForAdmin(
            admin.Principal(), counted, default, pageSize: 10, cursor: cursor);

        second.Value<PaginatedResponse<AuditLogResponse>>().TotalCount.Should().BeNull(
            "a cursor page should not pay for a COUNT unless includeTotal is explicit");
        interceptor.CommandTexts.Should().NotContain(
            sql => sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase),
            "and it must not run one behind the caller's back either");
    }

    [Fact]
    public async Task Audits_CursorMode_VisitsEveryRowExactlyOnceThroughCollidingTimestamps()
    {
        // Every page boundary lands inside a run of rows sharing one CreatedAt,
        // so the Id tie-break is exercised on each page rather than occasionally.
        var db = CreateDbContext();
        var admin = await AddAuditRowsAsync(db, 30, collidingTimestamps: true);

        var seen = new HashSet<Guid>();
        string? cursor = null;
        do
        {
            var page = await AuditsEndpoint.ListForAdmin(
                admin.Principal(), CreateDbContext(), default, pageSize: 10, cursor: cursor);
            var body = page.Value<PaginatedResponse<AuditLogResponse>>();
            foreach (var row in body.Items)
                seen.Add(row.Id).Should().BeTrue("cursor traversal must never return the same row twice");
            cursor = body.NextCursor;
        } while (cursor is not null);

        seen.Should().HaveCount(30, "with no gaps at the page boundaries either");
    }

    private static async Task AddCompetedEventsAsync(
        AppDbContext db, User owner, User competitor, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var ev = new Event { Name = $"Event {Guid.NewGuid():N}", CreatedById = owner.Id, IsStarted = true };
            var game = new EventGame { Event = ev, CustomGameName = "Game", IsEnabled = true };
            var objective = new Objective { EventGame = game, Name = "Obj", Score = 10 };
            db.AddRange(
                ev,
                game,
                objective,
                new EventCompetitor { Event = ev, UserId = competitor.Id },
                new CompletedObjective { Objective = objective, UserId = competitor.Id });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<User> AddAuditRowsAsync(
        AppDbContext db, int count, bool collidingTimestamps = false)
    {
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Type = AuditEventTypes.EventCreated,
                ActorUserId = admin.Id,
                CreatedAt = collidingTimestamps ? baseTime : baseTime.AddSeconds(-i),
            });
        }
        await db.SaveChangesAsync();
        return admin;
    }
}
