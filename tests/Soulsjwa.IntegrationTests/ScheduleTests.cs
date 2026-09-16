using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Calendar.Endpoints;
using Soulsjwa.Api.Features.Calendar.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// An event's calendar entries, a competitor's planned runs, and the global
/// read aggregating both. Everything turns on stored instants and a query
/// window, which a <c>timestamptz</c> column and an overlap predicate decide —
/// so real Postgres is the only place these mean anything. The wire contract
/// stays in <c>Soulsjwa.ApiTests</c>.
/// </summary>
public class ScheduleTests : IntegrationTestBase
{
    [Theory]
    [InlineData("competitor")]
    [InlineData("owner")]
    [InlineData("moderator")]
    public async Task CreatePlannedRun_BySomeoneEntitledToEditThatCompetitor_Succeeds(string caller)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var moderator = await Fixtures.AddUserAsync(db, "mod");
        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = f.Event.Id,
            CompetitorUserId = f.Competitor.Id,
            ModeratorUserId = moderator.Id,
        });
        await db.SaveChangesAsync();
        var principal = caller switch
        {
            "owner" => f.Owner,
            "moderator" => moderator,
            _ => f.Competitor,
        };

        var result = await CreateRunAsync(CreateDbContext(), f, principal);

        result.Status().Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task CreatePlannedRun_ForSomeoneElsesSlot_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = stranger.Id });
        await db.SaveChangesAsync();

        var result = await CreateRunAsync(CreateDbContext(), f, stranger);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CreatePlannedRun_EndingBeforeItStarts_IsRejected()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await CreateRunAsync(
            db, f, f.Competitor,
            startsAt: new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero),
            endsAt: new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreatePlannedRun_WithAnOffsetInstant_StoresItInUtc()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        // 20:00 at +02:00 is 18:00 UTC.
        var result = await CreateRunAsync(
            db, f, f.Competitor,
            startsAt: new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.FromHours(2)),
            endsAt: new DateTimeOffset(2026, 10, 1, 22, 0, 0, TimeSpan.Zero));

        result.Status().Should().Be(StatusCodes.Status201Created);
        result.Value<PlannedRunResponse>().StartsAt
            .Should().Be(new DateTime(2026, 10, 1, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task PlannedRuns_AreListedThenDeletable()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var runId = (await CreateRunAsync(db, f, f.Competitor)).Value<PlannedRunResponse>().Id;

        var listed = await PlannedRunsEndpoint.List(f.Event.Id, f.Competitor.Id, CreateDbContext(), default);
        listed.Value<List<PlannedRunResponse>>().Should().ContainSingle(r => r.Id == runId);

        var deleted = await PlannedRunsEndpoint.Delete(
            f.Event.Id, f.Competitor.Id, runId, f.Competitor.Principal(),
            CreateDbContext(), Audit, Cache, default);

        deleted.Status().Should().Be(StatusCodes.Status204NoContent);
        (await CreateDbContext().PlannedRuns.AnyAsync(r => r.Id == runId)).Should().BeFalse();
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    public async Task CreateCalendarEntry_BySomeoneEntitledToWriteIt_Succeeds(string caller)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await CreateEntryAsync(CreateDbContext(), f, caller == "admin" ? admin : f.Owner);

        result.Status().Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task CreateCalendarEntry_ByAStranger_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await CreateEntryAsync(db, f, stranger);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CreateCalendarEntry_EndingBeforeItStarts_IsRejected()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await CreateEntryAsync(
            db, f, f.Owner,
            startsAt: new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero),
            endsAt: new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateCalendarEntry_WithAColourThatIsNotOneOfTheNamedOnes_IsRejected()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await CreateEntryAsync(db, f, f.Owner, color: "Gold");

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateCalendarEntry_WithAnOffsetInstant_StoresItInUtc()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await CreateEntryAsync(
            db, f, f.Owner,
            startsAt: new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.FromHours(2)),
            endsAt: new DateTimeOffset(2026, 10, 1, 22, 0, 0, TimeSpan.Zero));

        result.Status().Should().Be(StatusCodes.Status201Created);
        result.Value<CalendarEntryResponse>().StartsAt
            .Should().Be(new DateTime(2026, 10, 1, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CalendarEntries_RoundTripTheAllDayFlagThenDelete()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var created = await CreateEntryAsync(db, f, f.Owner, isAllDay: true);
        var entry = created.Value<CalendarEntryResponse>();
        entry.IsAllDay.Should().BeTrue();

        var listed = await CalendarEntriesEndpoint.List(f.Event.Id, CreateDbContext(), default);
        listed.Value<List<CalendarEntryResponse>>().Should().ContainSingle(e => e.Id == entry.Id);

        var deleted = await CalendarEntriesEndpoint.Delete(
            f.Event.Id, entry.Id, f.Owner.Principal(), CreateDbContext(), Audit, Cache, default);

        deleted.Status().Should().Be(StatusCodes.Status204NoContent);
        (await CreateDbContext().CalendarEntries.AnyAsync(e => e.Id == entry.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GlobalCalendar_AggregatesEntriesAndPlannedRunsAcrossEvents()
    {
        var db = CreateDbContext();
        var first = await Fixtures.AddEventAsync(db);
        var second = await Fixtures.AddEventAsync(db);
        await AddEntryRowAsync(db, first, "First event entry", DateTime.UtcNow.AddDays(1));
        await AddEntryRowAsync(db, second, "Second event entry", DateTime.UtcNow.AddDays(2));
        await CreateRunAsync(CreateDbContext(), first, first.Competitor,
            startsAt: DateTimeOffset.UtcNow.AddDays(3), endsAt: DateTimeOffset.UtcNow.AddDays(3).AddHours(2));

        var calendar = await GlobalAsync();

        calendar.Entries.Select(e => e.Title)
            .Should().Contain("First event entry").And.Contain("Second event entry");
        calendar.PlannedRuns.Should().ContainSingle();
    }

    [Fact]
    public async Task GlobalCalendar_LeavesArchivedEventsOut()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        await AddEntryRowAsync(db, f, "Archived event entry", DateTime.UtcNow.AddDays(1));
        (await db.Events.SingleAsync(e => e.Id == f.Event.Id)).IsArchived = true;
        await db.SaveChangesAsync();

        var calendar = await GlobalAsync();

        calendar.Entries.Should().NotContain(e => e.Title == "Archived event entry",
            "the query filter on Event propagates through the required navigation");
    }

    [Fact]
    public async Task GlobalCalendar_LeavesEntriesOutsideTheDefaultWindowOut()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        await AddEntryRowAsync(db, f, "Far future entry", DateTime.UtcNow.AddDays(200));

        var calendar = await GlobalAsync();

        calendar.Entries.Should().NotContain(e => e.Title == "Far future entry");
    }

    [Fact]
    public async Task GlobalCalendar_KeepsAnEntryThatMerelyOverlapsTheWindow()
    {
        // Overlap, not containment: an entry that started before the window
        // and ends inside it is still happening as far as a reader is
        // concerned.
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        await AddEntryRowAsync(
            db, f, "Spans boundary", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-1));

        var calendar = await GlobalAsync();

        calendar.Entries.Should().Contain(e => e.Title == "Spans boundary");
    }

    [Fact]
    public async Task GlobalCalendar_CarriesEnoughToOpenAnEntryWithoutASecondRequest()
    {
        // The global read feeds both the month grid and the dialog that opens
        // from it, so an entry arrives with its markdown body and a planned
        // run with its colour — the dialog would otherwise need a per-event
        // round trip to render either.
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var entry = await AddEntryRowAsync(db, f, "Has a body", DateTime.UtcNow.AddDays(1));
        entry.DescriptionMarkdown = "# The body";
        await db.SaveChangesAsync();
        await CreateRunAsync(CreateDbContext(), f, f.Competitor,
            startsAt: DateTimeOffset.UtcNow.AddDays(2), endsAt: DateTimeOffset.UtcNow.AddDays(2).AddHours(2));

        var calendar = await GlobalAsync();

        calendar.Entries.Should().ContainSingle(e => e.Title == "Has a body")
            .Which.DescriptionMarkdown.Should().Be("# The body");
        calendar.PlannedRuns.Should().ContainSingle()
            .Which.Color.Should().Be(nameof(CalendarEntryColor.Default),
                "planned runs repaint with the same named slots as entries");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GlobalCalendar_WithAnUnusableWindow_IsRejected(bool tooWide)
    {
        var from = DateTimeOffset.UtcNow;
        var to = tooWide ? from.AddDays(GlobalCalendarEndpoint.MaxWindowDays + 1) : from.AddDays(-1);

        var result = await GlobalCalendarEndpoint.GetGlobalCalendar(CreateDbContext(), default, from, to);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    private Task<IResult> CreateRunAsync(
        AppDbContext db,
        EventFixture f,
        User caller,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null) =>
        PlannedRunsEndpoint.Create(
            f.Event.Id,
            f.Competitor.Id,
            new CreatePlannedRunRequest(
                f.Game.Id,
                startsAt ?? new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero),
                endsAt ?? new DateTimeOffset(2026, 10, 1, 22, 0, 0, TimeSpan.Zero)),
            caller.Principal(), db, Audit, Cache, default);

    private Task<IResult> CreateEntryAsync(
        AppDbContext db,
        EventFixture f,
        User caller,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        string color = "Default",
        bool isAllDay = false) =>
        CalendarEntriesEndpoint.Create(
            f.Event.Id,
            new CreateCalendarEntryRequest(
                "Entry",
                null,
                startsAt ?? new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero),
                endsAt ?? new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero),
                isAllDay,
                false,
                color,
                null),
            caller.Principal(), db, Audit, Cache, default);

    private static async Task<CalendarEntry> AddEntryRowAsync(
        AppDbContext db, EventFixture f, string title, DateTime startsAt, DateTime? endsAt = null)
    {
        var entry = new CalendarEntry
        {
            EventId = f.Event.Id,
            Title = title,
            StartsAt = startsAt,
            EndsAt = endsAt ?? startsAt.AddHours(1),
            CreatedById = f.Owner.Id,
        };
        db.CalendarEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private async Task<GlobalCalendarResponse> GlobalAsync()
    {
        var result = await GlobalCalendarEndpoint.GetGlobalCalendar(CreateDbContext(), default);
        return result.Value<GlobalCalendarResponse>();
    }
}
