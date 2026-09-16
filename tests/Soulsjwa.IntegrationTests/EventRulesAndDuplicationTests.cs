using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The rules document's size cap and the audit row that must not duplicate it,
/// plus the exact set of columns and children an event duplication carries over.
/// The wire contract is <c>Soulsjwa.ApiTests.EventRulesEndpointTests</c> and
/// <c>EventDuplicationEndpointTests</c>.
/// </summary>
public class EventRulesAndDuplicationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetRules_OnAnEventWithNone_ReportsNoContentRatherThanFailing()
    {
        var db = CreateDbContext();
        var (_, ev) = await AddEventAsync(db);

        var result = await GetRulesAsync(ev);

        result.Status().Should().Be(StatusCodes.Status200OK);
        result.Value<EventRulesResponse>().Content.Should().BeNull();
    }

    [Fact]
    public async Task GetRules_ForAnEventThatDoesNotExist_IsNotFound()
    {
        var result = await EventRulesEndpoint.GetRules(
            Guid.NewGuid(), CreateDbContext(), new DefaultHttpContext(), default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task PutRules_ByAnAdminWhoDoesNotOwnTheEvent_PersistsAndReadsBack()
    {
        var db = CreateDbContext();
        var (_, ev) = await AddEventAsync(db);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        const string content = "# Rules\n\nNo item duping.";

        var result = await PutRulesAsync(CreateDbContext(), ev, admin, content);

        result.Status().Should().Be(StatusCodes.Status200OK);
        result.Value<EventRulesResponse>().Content.Should().Be(content);
        (await GetRulesAsync(ev)).Value<EventRulesResponse>().Content.Should().Be(content);
    }

    [Fact]
    public async Task PutRules_ByAStranger_IsForbidden()
    {
        var db = CreateDbContext();
        var (_, ev) = await AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await PutRulesAsync(db, ev, stranger, "No item duping.");

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task PutRules_ForAnEventThatDoesNotExist_IsNotFound()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await EventRulesEndpoint.UpdateRules(
            Guid.NewGuid(), new UpdateEventRulesRequest("x"), admin.Principal(),
            db, Audit, new DefaultHttpContext(), default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task PutRules_AtTheByteLimit_IsAccepted_AndOverIt_IsNot()
    {
        var db = CreateDbContext();
        var (owner, ev) = await AddEventAsync(db);

        var atLimit = new string('a', EventRulesEndpoint.MaxContentBytes);
        (await PutRulesAsync(CreateDbContext(), ev, owner, atLimit))
            .Status().Should().Be(StatusCodes.Status200OK);

        var overLimit = new string('a', EventRulesEndpoint.MaxContentBytes + 1);
        var refused = await PutRulesAsync(CreateDbContext(), ev, owner, overLimit);

        refused.Status().Should().Be(StatusCodes.Status400BadRequest);
        refused.ValidationErrors().Should().ContainKey("Content");
    }

    [Fact]
    public async Task PutRules_AuditsADigestRatherThanTheWholeDocument()
    {
        // The audit row used to duplicate the full 64 KiB document on both
        // sides of the change. It must record a digest and a length instead, so
        // the row stays a few hundred bytes whatever the document weighs.
        var db = CreateDbContext();
        var (owner, ev) = await AddEventAsync(db);
        var atLimit = new string('a', EventRulesEndpoint.MaxContentBytes);

        (await PutRulesAsync(CreateDbContext(), ev, owner, atLimit))
            .Status().Should().Be(StatusCodes.Status200OK);

        var audit = await CreateDbContext().AuditLogs
            .SingleAsync(a => a.Type == AuditEventTypes.EventRulesUpdated && a.EventId == ev.Id);
        audit.AfterJson!.Length.Should().BeLessThan(500);
        audit.AfterJson.Should().NotContain(atLimit);
    }

    [Fact]
    public async Task Duplicate_CopiesTheGamesAndObjectivesAndNothingElse()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 7);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = f.Objective.Id,
            UserId = f.Competitor.Id,
        });
        await db.SaveChangesAsync();

        var result = await DuplicateAsync(CreateDbContext(), f.Event, admin);

        result.Status().Should().Be(StatusCodes.Status201Created);
        var copyId = result.Value<EventResponse>().Id;
        var after = CreateDbContext();
        var copiedGames = await after.EventGames.Where(eg => eg.EventId == copyId).ToListAsync();
        copiedGames.Should().ContainSingle();
        (await after.Objectives.CountAsync(o => o.EventGameId == copiedGames[0].Id)).Should().Be(1);
        (await after.EventCompetitors.AnyAsync(c => c.EventId == copyId)).Should().BeFalse(
            "a duplicate is a template, not a rerun with the same field");
        (await after.CompletedObjectives.CountAsync()).Should().Be(1, "progress is not copied");
    }

    [Fact]
    public async Task Duplicate_StartsTheCopyFresh()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: true);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var source = await db.Events.SingleAsync(e => e.Id == f.Event.Id);
        source.IsFeatured = true;
        source.UrlAlias = "source-alias";
        await db.SaveChangesAsync();

        var result = await DuplicateAsync(CreateDbContext(), f.Event, admin);

        var copy = result.Value<EventResponse>();
        copy.IsStarted.Should().BeFalse();
        copy.IsArchived.Should().BeFalse();
        copy.IsFeatured.Should().BeFalse("only one event may be featured");
        copy.UrlAlias.Should().BeNull("aliases are unique");
        copy.Games.Should().OnlyContain(g => !g.IsEnabled,
            "an enabled game on a not-yet-started event is a state no other path can reach");
    }

    [Fact]
    public async Task Duplicate_LeavesTheSourceExactlyAsItWas()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: true);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var before = await SnapshotAsync(f.Event.Id);

        await DuplicateAsync(CreateDbContext(), f.Event, admin);

        (await SnapshotAsync(f.Event.Id)).Should().BeEquivalentTo(before,
            "duplicating must not touch the event being copied");
    }

    [Fact]
    public async Task Duplicate_OfAnArchivedEvent_ProducesAnUnarchivedCopy()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        (await db.Events.SingleAsync(e => e.Id == f.Event.Id)).IsArchived = true;
        await db.SaveChangesAsync();

        var result = await DuplicateAsync(CreateDbContext(), f.Event, admin);

        result.Status().Should().Be(StatusCodes.Status201Created);
        result.Value<EventResponse>().IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_ByANonAdmin_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await DuplicateAsync(db, f.Event, f.Owner);

        result.Status().Should().Be(StatusCodes.Status403Forbidden,
            "duplicating creates an event, and creating events is admin-only");
    }

    [Fact]
    public async Task Duplicate_OfAnEventThatDoesNotExist_IsNotFound()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await EventDuplicationEndpoint.DuplicateEvent(
            Guid.NewGuid(), admin.Principal(), db, Audit, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    private static async Task<(User Owner, Event Event)> AddEventAsync(AppDbContext db)
    {
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = new Event { Name = "ev", CreatedById = owner.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (owner, ev);
    }

    private Task<IResult> GetRulesAsync(Event ev) =>
        EventRulesEndpoint.GetRules(ev.Id, CreateDbContext(), new DefaultHttpContext(), default);

    private Task<IResult> PutRulesAsync(AppDbContext db, Event ev, User caller, string content) =>
        EventRulesEndpoint.UpdateRules(
            ev.Id, new UpdateEventRulesRequest(content), caller.Principal(),
            db, Audit, new DefaultHttpContext(), default);

    private Task<IResult> DuplicateAsync(AppDbContext db, Event ev, User caller) =>
        EventDuplicationEndpoint.DuplicateEvent(ev.Id, caller.Principal(), db, Audit, default);

    /// <summary>
    /// The source event and its children as data, so "unchanged" can be
    /// asserted as a whole rather than field by field.
    /// </summary>
    private async Task<object> SnapshotAsync(Guid eventId)
    {
        var db = CreateDbContext();
        var ev = await db.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == eventId);
        var games = await db.EventGames.AsNoTracking()
            .Where(eg => eg.EventId == eventId)
            .OrderBy(eg => eg.SortOrder)
            .Select(eg => new { eg.Id, eg.CustomGameName, eg.KnownGameId, eg.IsEnabled, eg.SortOrder })
            .ToListAsync();
        var objectives = await db.Objectives.AsNoTracking()
            .Where(o => o.EventGame!.EventId == eventId)
            .OrderBy(o => o.Name)
            .Select(o => new { o.Id, o.Name, o.Score, o.Rule, o.FailRule })
            .ToListAsync();
        return new
        {
            ev.Name,
            ev.UrlAlias,
            ev.IsStarted,
            ev.IsArchived,
            ev.IsFeatured,
            ev.UpdatedAt,
            Games = games,
            Objectives = objectives,
        };
    }
}
