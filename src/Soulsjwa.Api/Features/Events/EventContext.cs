using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events;

/// <summary>
/// Shared endpoint-preamble loaders for "load the event, check it exists, check
/// ownership, load the game". Hand-rolled copies of this sequence drifted apart
/// (query filters applied inconsistently, <c>IsStarted</c> sometimes skipped);
/// use these instead of copying the pattern again.
/// </summary>
public static class EventContext
{
    public static async Task<(Event? Event, IResult? Error)> RequireEventAsync(
        Guid eventId, AppDbContext db, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return (null, Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound));

        return (ev, null);
    }

    /// <summary>
    /// Loads the event for a write that requires a live, running event.
    /// Archived events resolve to 404 — they are soft-deleted and must look
    /// absent to every write path, so this never bypasses the <c>IsArchived</c>
    /// query filter. Not-started events resolve to 403.
    /// </summary>
    public static async Task<(Event? Event, IResult? Error)> RequireRunningEventAsync(
        Guid eventId, AppDbContext db, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return (null, Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound));

        if (!ev.IsStarted)
            return (null, Results.Problem(detail: "Event is not started.", statusCode: StatusCodes.Status403Forbidden));

        return (ev, null);
    }

    /// <summary>
    /// Loads the event and requires the caller to be its owner or an admin.
    /// <paramref name="action"/> is interpolated by
    /// <see cref="EventOwnership.RequireOwner"/> as "Only the event owner can
    /// {action}."
    /// </summary>
    public static async Task<(Event? Event, IResult? Error)> RequireOwnedEventAsync(
        Guid eventId, ClaimsPrincipal principal, string action, AppDbContext db, CancellationToken ct)
    {
        var (ev, error) = await RequireEventAsync(eventId, db, ct);
        if (error is not null)
            return (null, error);

        if (EventOwnership.RequireOwner(ev!, principal, action) is { } ownerError)
            return (null, ownerError);

        return (ev, null);
    }

    public static async Task<(Event? Event, EventGame? EventGame, IResult? Error)> RequireEventGameAsync(
        Guid eventId, Guid eventGameId, AppDbContext db, CancellationToken ct)
    {
        var (ev, error) = await RequireEventAsync(eventId, db, ct);
        if (error is not null)
            return (null, null, error);

        var eventGame = await db.EventGames.FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);
        if (eventGame is null)
            return (ev, null, Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound));

        return (ev, eventGame, null);
    }
}
