using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events;

/// <summary>
/// Per-event authorization rules, centralised so every endpoint produces
/// consistent 403 responses.
///
/// Permission hierarchy (broadest first):
/// - Admin: anything on any event.
/// - Owner (creator): manage event content.
/// - Streamer competitor: delegate moderators.
/// - Moderator: mark objectives complete on behalf of competitors, for the
///   specific event they were delegated for.
/// - Competitor: mark their own objectives complete.
/// </summary>
public static class EventOwnership
{
    public const string RoleClaim = "role";

    /// <summary>
    /// Throws when the NameIdentifier claim is missing or malformed — unreachable
    /// behind <c>RequireAuthorization()</c>, since both auth schemes guarantee a
    /// GUID subject. Use <see cref="TryGetUserId"/> off that path.
    /// </summary>
    public static Guid GetUserId(ClaimsPrincipal principal) =>
        TryGetUserId(principal, out var userId)
            ? userId
            : throw new FormatException("Principal's NameIdentifier claim is missing or not a valid GUID.");

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public static bool IsAdmin(ClaimsPrincipal principal)
    {
        // JWT inbound claim mapping rewrites "role" to ClaimTypes.Role, but
        // the API-key handler keeps the raw name, so we check both.
        var role = principal.FindFirstValue(RoleClaim) ?? principal.FindFirstValue(ClaimTypes.Role);
        return string.Equals(role, nameof(UserRole.Admin), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>null</c> if the principal may manage event content (admin or creator),
    /// otherwise a 403. <paramref name="action"/> is interpolated as
    /// "Only the event owner can {action}."
    /// </summary>
    public static IResult? RequireOwner(Event ev, ClaimsPrincipal principal, string action)
    {
        if (IsAdmin(principal)) return null;

        var userId = GetUserId(principal);
        if (ev.CreatedById == userId) return null;

        return Results.Problem(
            detail: $"Only the event owner can {action}.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// <c>null</c> if the principal may complete objectives on behalf of the
    /// target user: admin, or a moderator the target streamer competitor
    /// delegated for this specific event.
    /// </summary>
    public static async Task<IResult?> RequireCanCompleteForStreamerAsync(
        Event ev, ClaimsPrincipal principal, Guid targetStreamerId, AppDbContext db, CancellationToken ct = default)
    {
        if (IsAdmin(principal)) return null;

        var callerId = GetUserId(principal);

        var targetIsStreamer = await db.EventCompetitors
            .AnyAsync(c => c.EventId == ev.Id && c.UserId == targetStreamerId && c.IsStreamer, ct);
        if (!targetIsStreamer)
            return Results.Problem(
                detail: "Target competitor is not marked as a streamer in this event.",
                statusCode: StatusCodes.Status403Forbidden);

        var isDelegatedMod = await db.EventCompetitorModerators.AnyAsync(
            m => m.EventId == ev.Id
              && m.CompetitorUserId == targetStreamerId
              && m.ModeratorUserId == callerId, ct);
        if (isDelegatedMod) return null;

        return Results.Problem(
            detail: "Only an admin or a moderator delegated by this streamer can complete objectives on their behalf.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// <c>null</c> if the principal may manage competitor metadata (death clips,
    /// links, notes) for the target competitor: admin, event owner, the target
    /// competitor themselves, or a moderator they delegated for this event.
    /// </summary>
    public static async Task<IResult?> RequireCanEditCompetitorInfoAsync(
        Event ev, ClaimsPrincipal principal, Guid targetUserId, AppDbContext db, CancellationToken ct = default)
    {
        if (IsAdmin(principal)) return null;

        var callerId = GetUserId(principal);
        if (ev.CreatedById == callerId) return null;
        if (callerId == targetUserId) return null;

        var isDelegatedMod = await db.EventCompetitorModerators.AnyAsync(
            m => m.EventId == ev.Id
              && m.CompetitorUserId == targetUserId
              && m.ModeratorUserId == callerId, ct);
        if (isDelegatedMod) return null;

        return Results.Problem(
            detail: "Only an admin, the event owner, the competitor themselves, or one of their delegated moderators can manage their info.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// <c>true</c> if the principal belongs to the event in any capacity —
    /// admin, creator, competitor, or a delegated moderator. Gates audit viewing
    /// and archived-event visibility. Safe to call with an unauthenticated
    /// principal (always <c>false</c>): some callers reach here through an
    /// <c>AllowAnonymous</c> endpoint.
    /// </summary>
    public static async Task<bool> IsEventMemberAsync(
        Event ev, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct = default)
    {
        if (principal.Identity?.IsAuthenticated != true) return false;
        if (IsAdmin(principal)) return true;

        var callerId = GetUserId(principal);
        if (ev.CreatedById == callerId) return true;

        if (await db.EventCompetitors.AnyAsync(c => c.EventId == ev.Id && c.UserId == callerId, ct))
            return true;
        if (await db.EventCompetitorModerators.AnyAsync(
                m => m.EventId == ev.Id && m.ModeratorUserId == callerId, ct))
            return true;

        return false;
    }
}
