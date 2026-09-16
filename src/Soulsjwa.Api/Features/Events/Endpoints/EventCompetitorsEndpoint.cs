using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Users.Endpoints;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

using Soulsjwa.Api.Features.Users;

namespace Soulsjwa.Api.Features.Events.Endpoints;

/// <summary>
/// Either <see cref="UserId"/> (existing user) or <see cref="TwitchLogin"/>
/// (raw handle, optionally one that has never signed in) must be supplied. If
/// the login does not yet match an existing user a placeholder user is
/// created and added to the allowlist so they can claim the row on first
/// Twitch login.
/// </summary>
public sealed record AddCompetitorRequest(Guid? UserId, string? TwitchLogin, bool IsStreamer = false);
public sealed record PatchCompetitorRequest(bool IsStreamer);

/// <summary>
/// An event's competitor roster: self-join, invitation by id or Twitch handle,
/// the streamer flag, and removal.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class EventCompetitorsEndpoint : IEndpoint
{
    private const string UsersTwitchIdIndexName = "IX_Users_TwitchId";
    private const string AllowlistedTwitchLoginIndexName = "IX_AllowlistedTwitchLogins_TwitchLogin";
    private const string EventCompetitorsPrimaryKeyName = "PK_EventCompetitors";

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/events/{eventId:guid}/competitors");

        group.MapPost("/", AddCompetitor)
            .WithName("AddCompetitor")
            .WithSummary("Adds a competitor to an event (owner only)")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPost("/self", SelfJoin)
            .WithName("SelfJoinEvent")
            .WithSummary("Adds the current user as a competitor")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapDelete("/{userId:guid}", RemoveCompetitor)
            .WithName("RemoveCompetitor")
            .WithSummary("Removes a competitor from an event (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPatch("/{userId:guid}", PatchCompetitor)
            .WithName("PatchCompetitor")
            .WithSummary("Updates a competitor's streamer delegation flag (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();
    }

    internal static async Task<IResult> SelfJoin(
        Guid eventId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventCompetitorsEndpoint> logger,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        if (ev.IsStarted)
            return Results.Problem(
                detail: "Cannot join while the event is running. Ask the event owner to add you as a competitor.",
                statusCode: StatusCodes.Status409Conflict);

        var userId = EventOwnership.GetUserId(principal);
        var alreadyAdded = await db.EventCompetitors
            .AnyAsync(ec => ec.EventId == eventId && ec.UserId == userId, ct);
        if (alreadyAdded)
            return Results.Problem(
                detail: "User is already a competitor in this event.",
                statusCode: StatusCodes.Status409Conflict);

        db.EventCompetitors.Add(new EventCompetitor
        {
            EventId = eventId,
            UserId = userId
        });

        audit.Log(db, AuditEventTypes.CompetitorAdded, userId,
            eventId: eventId, subjectUserId: userId,
            after: new { UserId = userId, SelfJoin = true });

        await db.SaveChangesAsync(ct);
        // The roster decides which rows the scoreboard has and therefore
        // everyone's rank, so a change must drop the cached payload — otherwise
        // the scoreboard, /scores and the overlay serve the old roster until the
        // cache policy expires.
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.CompetitorAdded(userId, eventId, userId);
        return Results.Created($"/api/v1/events/{eventId}/competitors/{userId}", null);
    }

    internal static async Task<IResult> AddCompetitor(
        Guid eventId,
        AddCompetitorRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventCompetitorsEndpoint> logger,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "add competitors", db, ct);
        if (error is not null) return error;

        string? login = null;
        if (request.UserId is not { } providedId || providedId == Guid.Empty)
        {
            if (string.IsNullOrWhiteSpace(request.TwitchLogin))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [""] = ["Provide either UserId or TwitchLogin."]
                });

            login = request.TwitchLogin.Trim().ToLowerInvariant();
            if (login.Length > 64)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["TwitchLogin"] = ["TwitchLogin must be 64 characters or fewer."]
                });
        }
        else if (!await db.Users.AnyAsync(u => u.Id == providedId, ct))
        {
            return Results.Problem(detail: "User not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var actorId = EventOwnership.GetUserId(principal);

        // The whole resolve-or-create + competitor-add + audit sequence commits
        // atomically: a partial failure must never leave a placeholder User or
        // AllowlistedTwitchLogin row granting sign-in rights without a completed
        // invitation. EF Core takes an implicit savepoint before each
        // SaveChangesAsync inside a user-managed transaction, so retrying once
        // after the unique-violation below rolls back only that attempt.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        Guid targetUserId;
        if (login is null)
        {
            targetUserId = request.UserId!.Value;
        }
        else
        {
            var existing = await db.Users.FirstOrDefaultAsync(u => u.TwitchLogin.ToLower() == login, ct);
            if (existing is not null)
            {
                targetUserId = existing.Id;
            }
            else
            {
                var created = await CreatePlaceholderAndAllowlistAsync(db, audit, login, actorId, principal, ct);
                if (created is null)
                    return Results.Problem(
                        detail: "Only admins can invite a Twitch handle that has never signed in. Ask an admin to allowlist it first.",
                        statusCode: StatusCodes.Status403Forbidden);
                targetUserId = created.Value;
            }
        }

        var alreadyAdded = await db.EventCompetitors
            .AnyAsync(ec => ec.EventId == eventId && ec.UserId == targetUserId, ct);
        if (alreadyAdded)
            return Results.Problem(
                detail: "User is already a competitor in this event.",
                statusCode: StatusCodes.Status409Conflict);

        db.EventCompetitors.Add(new EventCompetitor
        {
            EventId = eventId,
            UserId = targetUserId,
            IsStreamer = request.IsStreamer,
        });

        audit.Log(db, AuditEventTypes.CompetitorAdded, actorId,
            eventId: eventId, subjectUserId: targetUserId,
            after: new { UserId = targetUserId, request.TwitchLogin, request.IsStreamer });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: EventCompetitorsPrimaryKeyName,
            })
        {
            // A concurrent invite of the same handle to the same event won
            // the race to add the EventCompetitor row between our own
            // pre-check and this save.
            return Results.Problem(
                detail: "User is already a competitor in this event.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await transaction.CommitAsync(ct);

        // After the commit, never before: evicting for a write that then rolls
        // back drops a valid payload for nothing.
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);

        logger.CompetitorAdded(targetUserId, eventId, actorId);
        return Results.Created($"/api/v1/events/{eventId}/competitors/{targetUserId}", null);
    }

    /// <summary>
    /// Creates a placeholder <see cref="User"/> (adopted on first real Twitch
    /// login, see <see cref="PendingUserMarker"/>) plus an
    /// <see cref="AllowlistedTwitchLogin"/> entry for an unknown handle.
    /// Admin-only, so an owner who is later demoted cannot keep granting
    /// sign-in rights through this path; returns <c>null</c> otherwise. On a
    /// concurrent-invite race, detaches this attempt's speculative rows and
    /// resolves to the winner's row instead of throwing.
    /// </summary>
    private static async Task<Guid?> CreatePlaceholderAndAllowlistAsync(
        AppDbContext db, IAuditService audit, string login, Guid actorId, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return null;

        // The sentinel TwitchId lets the Twitch upsert flow adopt this row on
        // first real login while keeping the unique-TwitchId constraint
        // satisfied today.
        var placeholder = new User
        {
            TwitchId = PendingUserMarker.For(login),
            TwitchLogin = login,
            DisplayName = login,
            IsAllowlisted = true,
        };
        db.Users.Add(placeholder);

        // Allowlist the handle too so /admin/allowlist stays the canonical
        // place to see who may sign in.
        var alreadyAllowlisted = await db.AllowlistedTwitchLogins.AnyAsync(a => a.TwitchLogin == login, ct);
        var allowlistEntry = alreadyAllowlisted
            ? null
            : new AllowlistedTwitchLogin
            {
                TwitchLogin = login,
                Note = "Auto-added via competitor invite",
                AddedById = actorId,
            };
        if (allowlistEntry is not null) db.AllowlistedTwitchLogins.Add(allowlistEntry);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } pgException
            && (pgException.ConstraintName == UsersTwitchIdIndexName || pgException.ConstraintName == AllowlistedTwitchLoginIndexName))
        {
            db.Entry(placeholder).State = EntityState.Detached;
            if (allowlistEntry is not null) db.Entry(allowlistEntry).State = EntityState.Detached;

            var winner = await db.Users.FirstAsync(u => u.TwitchLogin.ToLower() == login, ct);
            return winner.Id;
        }

        if (allowlistEntry is not null)
        {
            audit.Log(db, AuditEventTypes.AllowlistAdded, actorId,
                subjectUserId: placeholder.Id,
                after: new { allowlistEntry.TwitchLogin, allowlistEntry.Note });
            await db.SaveChangesAsync(ct);
        }

        return placeholder.Id;
    }

    internal static async Task<IResult> PatchCompetitor(
        Guid eventId,
        Guid userId,
        PatchCompetitorRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventCompetitorsEndpoint> logger,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "update competitors", db, ct);
        if (error is not null) return error;

        var competitor = await db.EventCompetitors
            .FirstOrDefaultAsync(ec => ec.EventId == eventId && ec.UserId == userId, ct);
        if (competitor is null)
            return Results.Problem(detail: "Competitor not found.", statusCode: StatusCodes.Status404NotFound);

        if (competitor.IsStreamer == request.IsStreamer)
            return Results.NoContent();

        var before = new { competitor.UserId, competitor.IsStreamer };
        competitor.IsStreamer = request.IsStreamer;
        if (!request.IsStreamer)
        {
            var delegations = await db.EventCompetitorModerators
                .Where(m => m.EventId == eventId && m.CompetitorUserId == userId)
                .ToListAsync(ct);
            db.EventCompetitorModerators.RemoveRange(delegations);
        }

        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, request.IsStreamer ? AuditEventTypes.StreamerAdded : AuditEventTypes.StreamerRemoved, actorId,
            eventId: eventId, subjectUserId: userId,
            before: before,
            after: new { competitor.UserId, competitor.IsStreamer });
        await db.SaveChangesAsync(ct);
        if (request.IsStreamer)
            logger.StreamerAdded(userId, eventId, actorId);
        else
            logger.StreamerRemoved(userId, eventId, actorId);
        return Results.NoContent();
    }

    internal static async Task<IResult> RemoveCompetitor(
        Guid eventId,
        Guid userId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventCompetitorsEndpoint> logger,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "remove competitors", db, ct);
        if (error is not null) return error;

        var competitor = await db.EventCompetitors
            .FirstOrDefaultAsync(ec => ec.EventId == eventId && ec.UserId == userId, ct);

        if (competitor is null)
            return Results.Problem(detail: "Competitor not found.", statusCode: StatusCodes.Status404NotFound);

        db.EventCompetitors.Remove(competitor);
        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.CompetitorRemoved, actorId,
            eventId: eventId, subjectUserId: userId,
            before: new { UserId = userId });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.CompetitorRemoved(userId, eventId, actorId);
        return Results.NoContent();
    }
}
