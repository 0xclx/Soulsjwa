using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Common.Models;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Audits.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Audits.Endpoints;

public sealed record AuditUserSummary(Guid Id, string DisplayName, string TwitchLogin);

public sealed record AuditLogResponse(
    Guid Id,
    string Type,
    Guid? EventId,
    Guid? EventGameId,
    Guid? ObjectiveId,
    AuditUserSummary Actor,
    AuditUserSummary? Subject,
    string? BeforeJson,
    string? AfterJson,
    string? Reason,
    DateTime CreatedAt);

/// <summary>
/// Read-only audit-log endpoints. The per-event one is visible to anyone who
/// belongs to the event (admin, owner, competitor, streamer, delegated
/// moderator); the admin one also exposes audits that are not event-scoped
/// (role changes, allowlist mutations).
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public sealed class AuditsEndpoint : IEndpoint
{
    private static readonly int[] AllowedPageSizes = [10, 20, 30, 40, 50];
    private const int DefaultPageSize = 20;

    /// <summary>
    /// Offset pagination beyond this page is rejected: OFFSET cost on the
    /// append-only, unbounded AuditLogs table grows linearly with depth. Deeper
    /// callers must use the cursor parameter, which costs the same at any depth.
    /// </summary>
    private const int MaxOffsetPage = 100;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/events/{eventId:guid}/audits", ListForEvent)
            .WithName("ListEventAudits")
            .WithSummary("Lists audit entries for an event. Visible to admins, the event owner, competitors, and delegated moderators.")
            .Produces<PaginatedResponse<AuditLogResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        app.MapGet(ApiRoutes.Prefix + "/admin/audits", ListForAdmin)
            .WithName("ListAdminAudits")
            .WithSummary("Lists all audit entries (admin only) with optional filters")
            .Produces<PaginatedResponse<AuditLogResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAdmin();
    }

    internal static async Task<IResult> ListForEvent(
        Guid eventId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct,
        int page = 1,
        int pageSize = DefaultPageSize,
        string? cursor = null,
        bool? includeTotal = null,
        string[]? type = null,
        Guid? actorUserId = null,
        Guid? subjectUserId = null,
        Guid? eventGameId = null,
        Guid? objectiveId = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        if (!await EventOwnership.IsEventMemberAsync(ev!, principal, db, ct))
            return Results.Problem(
                detail: "Only members of this event can view its audit history.",
                statusCode: StatusCodes.Status403Forbidden);

        var query = db.AuditLogs.AsNoTracking().Where(a => a.EventId == eventId);
        return await PaginateAsync(query, page, pageSize, cursor, includeTotal, type, actorUserId, subjectUserId,
            eventGameId, objectiveId, from, to, db, ct);
    }

    internal static async Task<IResult> ListForAdmin(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct,
        int page = 1,
        int pageSize = DefaultPageSize,
        string? cursor = null,
        bool? includeTotal = null,
        string[]? type = null,
        Guid? actorUserId = null,
        Guid? subjectUserId = null,
        Guid? eventId = null,
        Guid? eventGameId = null,
        Guid? objectiveId = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (eventId.HasValue)
            query = query.Where(a => a.EventId == eventId.Value);

        return await PaginateAsync(query, page, pageSize, cursor, includeTotal, type, actorUserId, subjectUserId,
            eventGameId, objectiveId, from, to, db, ct);
    }

    private static async Task<IResult> PaginateAsync(
        IQueryable<AuditLog> query,
        int page,
        int pageSize,
        string? cursor,
        bool? includeTotal,
        string[]? types,
        Guid? actorUserId,
        Guid? subjectUserId,
        Guid? eventGameId,
        Guid? objectiveId,
        DateTime? from,
        DateTime? to,
        AppDbContext db,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        // Snap to the nearest allowed page size so weird URL hand-edits still
        // give a sensible response instead of a 400.
        if (!AllowedPageSizes.Contains(pageSize))
            pageSize = DefaultPageSize;

        if (cursor is null && page > MaxOffsetPage)
            return Results.Problem(
                detail: $"Page {page} exceeds the maximum of {MaxOffsetPage}. Use the cursor parameter to page further.",
                statusCode: StatusCodes.Status400BadRequest);

        var cursorCreatedAt = default(DateTime);
        var cursorId = default(Guid);
        if (cursor is not null && !AuditCursor.TryDecode(cursor, out cursorCreatedAt, out cursorId))
            return Results.Problem(detail: "Invalid cursor.", statusCode: StatusCodes.Status400BadRequest);

        // Multi-value `type=a&type=b` filter: rows matching ANY supplied type.
        // Empty values are ignored so a stray `&type=` doesn't filter everything away.
        if (types is { Length: > 0 })
        {
            var normalized = types
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length > 0)
                query = query.Where(a => normalized.Contains(a.Type));
        }
        if (actorUserId.HasValue)
            query = query.Where(a => a.ActorUserId == actorUserId.Value);
        if (subjectUserId.HasValue)
            query = query.Where(a => a.SubjectUserId == subjectUserId.Value);
        if (eventGameId.HasValue)
            query = query.Where(a => a.EventGameId == eventGameId.Value);
        if (objectiveId.HasValue)
            query = query.Where(a => a.ObjectiveId == objectiveId.Value);
        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt <= toUtc);
        }

        // Cursor mode replaces the offset with a tuple comparison mirroring
        // the sort exactly, so rows are neither skipped nor repeated at page
        // boundaries even when many rows share a CreatedAt.
        if (cursor is not null)
        {
            query = query.Where(a => a.CreatedAt < cursorCreatedAt
                || (a.CreatedAt == cursorCreatedAt && a.Id < cursorId));
        }

        // The COUNT scans the whole filtered set — skip it for cursor pages
        // (the whole point of avoiding OFFSET) unless explicitly requested.
        var shouldIncludeTotal = includeTotal ?? cursor is null;
        int? total = shouldIncludeTotal ? await query.CountAsync(ct) : null;

        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip(cursor is null ? (page - 1) * pageSize : 0)
            .Take(pageSize)
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.ActorUserId)
            .Concat(rows.Where(r => r.SubjectUserId.HasValue).Select(r => r.SubjectUserId!.Value))
            .Distinct()
            .ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new AuditUserSummary(u.Id, u.DisplayName, u.TwitchLogin))
            .ToListAsync(ct);
        var userMap = users.ToDictionary(u => u.Id);

        AuditUserSummary GetSummary(Guid id) =>
            userMap.TryGetValue(id, out var s)
                ? s
                : new AuditUserSummary(id, "(deleted user)", string.Empty);

        var items = rows.Select(r => new AuditLogResponse(
            r.Id,
            r.Type,
            r.EventId,
            r.EventGameId,
            r.ObjectiveId,
            GetSummary(r.ActorUserId),
            r.SubjectUserId.HasValue ? GetSummary(r.SubjectUserId.Value) : null,
            r.BeforeJson,
            r.AfterJson,
            r.Reason,
            r.CreatedAt)).ToList();

        // A full page means there may be more; a short page is necessarily the
        // last. Populated regardless of the mode used, so a page-based caller can
        // switch to cursor-based "load more".
        var nextCursor = rows.Count == pageSize
            ? AuditCursor.Encode(rows[^1].CreatedAt, rows[^1].Id)
            : null;

        return Results.Ok(new PaginatedResponse<AuditLogResponse>(items, total, page, pageSize, nextCursor));
    }

    /// <summary>
    /// Encodes/decodes the opaque keyset cursor for <see cref="PaginateAsync"/>:
    /// the last row's <c>(CreatedAt, Id)</c>, the same tuple the results are
    /// ordered by, so resuming from it can neither skip nor repeat a row.
    /// </summary>
    private static class AuditCursor
    {
        public static string Encode(DateTime createdAt, Guid id) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt.Ticks}_{id}"));

        public static bool TryDecode(string cursor, out DateTime createdAt, out Guid id)
        {
            createdAt = default;
            id = default;
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                var parts = decoded.Split('_', 2);
                if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParse(parts[1], out id))
                    return false;

                createdAt = new DateTime(ticks, DateTimeKind.Utc);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
