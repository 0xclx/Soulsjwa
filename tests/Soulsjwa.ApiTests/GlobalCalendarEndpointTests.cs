using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Calendar.Endpoints;
using Soulsjwa.Api.Features.Calendar.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The global calendar over the wire: the anonymous read, the payload shape,
/// and the output cache being evicted when an entry changes — which only exists
/// in the HTTP pipeline. The window rules and the archived-event exclusion live
/// in <c>Soulsjwa.IntegrationTests.ScheduleTests</c>.
/// </summary>
public class GlobalCalendarEndpointTests : ApiTestBase
{
    [Fact]
    public async Task GetGlobalCalendar_AggregatesEntriesAndPlannedRunsAcrossEvents()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, "Event A", comp.Id);
        await SeedCalendarEntryAsync(ev.Id, owner.Id, "Grand Finals");
        await SeedPlannedRunAsync(ev.Id, eg.Id, comp.Id);

        var anon = Factory.CreateClient();
        var calendar = await anon.GetFromJsonAsync<CalendarDto>("/api/v1/calendar");

        calendar!.Entries.Should().ContainSingle(e => e.Title == "Grand Finals" && e.EventName == "Event A");
        calendar.PlannedRuns.Should().ContainSingle(r => r.EventId == ev.Id && r.UserId == comp.Id);
    }

    [Fact]
    public async Task GetGlobalCalendar_IsAnonymouslyReadable()
    {
        var anon = Factory.CreateClient();
        var response = await anon.GetAsync("/api/v1/calendar");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetGlobalCalendar_ResponseIncludesTheDescriptionMarkdown()
    {
        // The dialog that opens from the month grid renders the body, so the
        // strip carries it rather than costing a per-event round trip. It was
        // once deliberately withheld, so the field name is pinned here.
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = (await SeedEventWithGameAsync(owner.Id, "Body Event", owner.Id)).Ev;

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CalendarEntries.Add(new CalendarEntry
            {
                EventId = ev.Id,
                Title = "Has a body",
                DescriptionMarkdown = "# Shown in the dialog",
                StartsAt = DateTime.UtcNow,
                EndsAt = DateTime.UtcNow.AddHours(1),
                CreatedById = owner.Id,
            });
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/v1/calendar");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entry = body.GetProperty("entries").EnumerateArray()
            .Single(e => e.GetProperty("title").GetString() == "Has a body");
        entry.GetProperty("descriptionMarkdown").GetString().Should().Be("# Shown in the dialog");
    }

    [Fact]
    public async Task CreatingCalendarEntry_EvictsGlobalCalendarCache()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = (await SeedEventWithGameAsync(owner.Id, "Cache Evict Event", owner.Id)).Ev;
        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);

        // Warm the cache.
        await Client.GetAsync("/api/v1/calendar");

        var create = await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/calendar-entries", new
        {
            title = "Freshly added",
            startsAt = DateTime.UtcNow,
            endsAt = DateTime.UtcNow.AddHours(1),
            isAllDay = false,
            isHighlighted = false,
            color = "Default",
        });
        create.EnsureSuccessStatusCode();

        var calendar = await Client.GetFromJsonAsync<CalendarDto>("/api/v1/calendar");
        calendar!.Entries.Should().Contain(e => e.Title == "Freshly added",
            "the create must have evicted the cached global calendar response");
    }

    private async Task<(Event Ev, EventGame Eg)> SeedEventWithGameAsync(Guid ownerId, string eventName, Guid competitorId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = eventName, CreatedById = ownerId };
        db.Events.Add(ev);
        var eg = new EventGame { EventId = ev.Id, CustomGameName = "Custom", IsEnabled = true };
        db.EventGames.Add(eg);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = competitorId });
        await db.SaveChangesAsync();
        return (ev, eg);
    }

    private async Task SeedCalendarEntryAsync(Guid eventId, Guid createdById, string title)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CalendarEntries.Add(new CalendarEntry
        {
            EventId = eventId,
            Title = title,
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddHours(1),
            CreatedById = createdById,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedPlannedRunAsync(Guid eventId, Guid eventGameId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PlannedRuns.Add(new PlannedRun
        {
            EventId = eventId,
            EventGameId = eventGameId,
            UserId = userId,
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync();
    }

    private sealed record CalendarDto(List<EntryDto> Entries, List<RunDto> PlannedRuns);
    private sealed record EntryDto(Guid Id, Guid EventId, string EventName, string Title);
    private sealed record RunDto(Guid Id, Guid EventId, string EventName, Guid EventGameId, string GameName, Guid UserId, string CompetitorName);
}
