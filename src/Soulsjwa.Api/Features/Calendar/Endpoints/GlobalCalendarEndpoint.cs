using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Calendar.Endpoints;

public sealed record GlobalCalendarEntryResponse(
    Guid Id,
    Guid EventId,
    string EventName,
    string Title,
    string? DescriptionMarkdown,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsAllDay,
    bool IsHighlighted,
    string Color,
    Guid? ImageAssetId,
    string? ImageUrl);

public sealed record GlobalPlannedRunResponse(
    Guid Id,
    Guid EventId,
    string EventName,
    Guid EventGameId,
    string GameName,
    Guid UserId,
    string CompetitorName,
    DateTime StartsAt,
    DateTime EndsAt,
    string Color);

public sealed record GlobalCalendarResponse(
    List<GlobalCalendarEntryResponse> Entries,
    List<GlobalPlannedRunResponse> PlannedRuns,
    bool Truncated);

/// <summary>
/// Aggregates every non-archived event's calendar entries and planned runs
/// for the global `/calendar` page, bounded to a date window. Archived events
/// drop out automatically: <c>Event</c> has a global query filter on
/// <c>IsArchived</c> and both <see cref="Entities.CalendarEntry"/> and
/// <see cref="Entities.PlannedRun"/> have a required navigation to it, so EF
/// propagates that filter here with no extra condition.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class GlobalCalendarEndpoint : IEndpoint
{
    /// <summary>Widest window a caller may request in one call.</summary>
    public const int MaxWindowDays = 400;

    private const int DefaultLookbackDays = 7;

    private const int DefaultLookaheadDays = 60;

    /// <summary>Backstop cap on each collection, independent of the date window.</summary>
    public const int MaxEntries = 2000;
    public const int MaxPlannedRuns = 2000;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/calendar", GetGlobalCalendar)
            .WithName("GetGlobalCalendar")
            .WithSummary("Aggregates calendar entries and planned runs across every non-archived event, within a bounded date window")
            .Produces<GlobalCalendarResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .CacheOutput("Calendar")
            .AllowAnonymous();
    }

    internal static async Task<IResult> GetGlobalCalendar(
        AppDbContext db,
        CancellationToken ct,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var today = DateTime.UtcNow.Date;
        var windowStart = from?.UtcDateTime ?? today.AddDays(-DefaultLookbackDays);
        var windowEnd = to?.UtcDateTime ?? today.AddDays(DefaultLookaheadDays);

        if (windowEnd <= windowStart)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["to"] = ["to must be after from."],
            });

        if ((windowEnd - windowStart).TotalDays > MaxWindowDays)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["to"] = [$"The requested window must be {MaxWindowDays} days or narrower."],
            });

        // Overlap, not containment — an entry spanning the window boundary
        // must still be included.
        var entryQuery = db.CalendarEntries
            .AsNoTracking()
            .Where(e => e.StartsAt < windowEnd && e.EndsAt > windowStart)
            .OrderBy(e => e.StartsAt)
            .Take(MaxEntries + 1)
            .Select(e => new GlobalCalendarEntryResponse(
                e.Id,
                e.EventId,
                e.Event.Name,
                e.Title,
                e.DescriptionMarkdown,
                e.StartsAt,
                e.EndsAt,
                e.IsAllDay,
                e.IsHighlighted,
                e.Color.ToString(),
                e.ImageAssetId,
                e.ImageAssetId != null ? "/api/v1/media/" + e.ImageAssetId : null));

        var plannedRunQuery = db.PlannedRuns
            .AsNoTracking()
            .Where(r => r.StartsAt < windowEnd && r.EndsAt > windowStart)
            .OrderBy(r => r.StartsAt)
            .Take(MaxPlannedRuns + 1)
            .Select(r => new GlobalPlannedRunResponse(
                r.Id,
                r.EventId,
                r.Event.Name,
                r.EventGameId,
                r.EventGame.CustomGameName ?? (r.EventGame.KnownGame != null ? r.EventGame.KnownGame.Name : null) ?? "Unknown",
                r.UserId,
                r.User.DisplayName,
                r.StartsAt,
                r.EndsAt,
                r.Color.ToString()));

        var entries = await entryQuery.ToListAsync(ct);
        var plannedRuns = await plannedRunQuery.ToListAsync(ct);

        var truncated = entries.Count > MaxEntries || plannedRuns.Count > MaxPlannedRuns;
        if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        if (plannedRuns.Count > MaxPlannedRuns) plannedRuns.RemoveRange(MaxPlannedRuns, plannedRuns.Count - MaxPlannedRuns);

        return Results.Ok(new GlobalCalendarResponse(entries, plannedRuns, truncated));
    }
}
