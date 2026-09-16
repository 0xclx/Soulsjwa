using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record EventModeratorResponse(Guid UserId, string DisplayName, DateTime AddedAt);
public sealed record AddModeratorRequest(Guid UserId);

/// <summary>
/// Per-streamer-competitor-per-event moderator delegation. Each competitor
/// marked as a streamer independently decides which users may mark objectives
/// complete on their behalf for this event. Delegation does NOT carry across
/// events or across competitors — every (event, competitor, moderator) is its
/// own row.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class EventCompetitorModeratorsEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/events/{eventId:guid}/competitors/{competitorUserId:guid}/moderators");

        group.MapGet("/", List)
            .WithName("ListCompetitorModerators")
            .WithSummary("Lists the moderators a streamer competitor has delegated for an event")
            .Produces<List<EventModeratorResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/", Add)
            .WithName("AddCompetitorModerator")
            .WithSummary("Delegates a user to moderate on this streamer competitor's behalf for the event (competitor or admin)")
            .Produces<EventModeratorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapDelete("/{moderatorUserId:guid}", Remove)
            .WithName("RemoveCompetitorModerator")
            .WithSummary("Revokes a moderator delegation (competitor or admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    internal static async Task<IResult> List(
        Guid eventId, Guid competitorUserId, AppDbContext db, CancellationToken ct)
    {
        var competitorExists = await db.EventCompetitors
            .AnyAsync(s => s.EventId == eventId && s.UserId == competitorUserId && s.IsStreamer, ct);
        if (!competitorExists)
            return Results.Problem(detail: "Streamer competitor not found in this event.", statusCode: StatusCodes.Status404NotFound);

        var mods = await db.EventCompetitorModerators
            .Where(m => m.EventId == eventId && m.CompetitorUserId == competitorUserId)
            .OrderBy(m => m.AddedAt)
            .Select(m => new EventModeratorResponse(m.ModeratorUserId, m.Moderator.DisplayName, m.AddedAt))
            .ToListAsync(ct);

        return Results.Ok(mods);
    }

    internal static async Task<IResult> Add(
        Guid eventId,
        Guid competitorUserId,
        AddModeratorRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventCompetitorModeratorsEndpoint> logger,
        CancellationToken ct)
    {
        var competitor = await db.EventCompetitors
            .FirstOrDefaultAsync(s => s.EventId == eventId && s.UserId == competitorUserId, ct);
        if (competitor is null)
            return Results.Problem(detail: "Competitor not found in this event.", statusCode: StatusCodes.Status404NotFound);
        if (!competitor.IsStreamer)
            return Results.Problem(detail: "Competitor is not marked as a streamer in this event.", statusCode: StatusCodes.Status409Conflict);

        if (RequireCompetitorOrAdmin(competitorUserId, principal) is { } authError)
            return authError;

        var modUser = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (modUser is null)
            return Results.Problem(detail: "User not found.", statusCode: StatusCodes.Status404NotFound);

        // Moderators must be allowlisted and signed up before being delegated.
        if (!modUser.IsAllowlisted)
            return Results.Problem(
                detail: "Moderator must be allowlisted and signed up first.",
                statusCode: StatusCodes.Status409Conflict);

        var already = await db.EventCompetitorModerators
            .AnyAsync(m => m.EventId == eventId && m.CompetitorUserId == competitorUserId && m.ModeratorUserId == request.UserId, ct);
        if (already)
            return Results.Problem(
                detail: "User is already a moderator for this competitor in this event.",
                statusCode: StatusCodes.Status409Conflict);

        var mod = new EventCompetitorModerator
        {
            EventId = eventId,
            CompetitorUserId = competitorUserId,
            ModeratorUserId = request.UserId,
        };
        db.EventCompetitorModerators.Add(mod);
        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.ModeratorAdded, actorId,
            eventId: eventId, subjectUserId: request.UserId,
            after: new { CompetitorUserId = competitorUserId, ModeratorUserId = request.UserId });
        await db.SaveChangesAsync(ct);
        logger.ModeratorAdded(request.UserId, competitorUserId, eventId, actorId);

        return Results.Created(
            $"/api/v1/events/{eventId}/competitors/{competitorUserId}/moderators/{request.UserId}",
            new EventModeratorResponse(modUser.Id, modUser.DisplayName, mod.AddedAt));
    }

    internal static async Task<IResult> Remove(
        Guid eventId,
        Guid competitorUserId,
        Guid moderatorUserId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventCompetitorModeratorsEndpoint> logger,
        CancellationToken ct)
    {
        if (RequireCompetitorOrAdmin(competitorUserId, principal) is { } authError)
            return authError;

        var mod = await db.EventCompetitorModerators
            .FirstOrDefaultAsync(m => m.EventId == eventId
                                   && m.CompetitorUserId == competitorUserId
                                   && m.ModeratorUserId == moderatorUserId, ct);
        if (mod is null)
            return Results.Problem(detail: "Moderator delegation not found.", statusCode: StatusCodes.Status404NotFound);

        db.EventCompetitorModerators.Remove(mod);
        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.ModeratorRemoved, actorId,
            eventId: eventId, subjectUserId: moderatorUserId,
            before: new { CompetitorUserId = competitorUserId, ModeratorUserId = moderatorUserId });
        await db.SaveChangesAsync(ct);
        logger.ModeratorRemoved(moderatorUserId, competitorUserId, eventId, actorId);
        return Results.NoContent();
    }

    /// <summary>
    /// Only the streamer competitor whose delegation list this is, or an admin,
    /// may manage it. The event creator deliberately gets no implicit access to
    /// other competitors' moderator lists.
    /// </summary>
    private static IResult? RequireCompetitorOrAdmin(Guid competitorUserId, ClaimsPrincipal principal)
    {
        if (EventOwnership.IsAdmin(principal)) return null;
        var callerId = EventOwnership.GetUserId(principal);
        if (callerId == competitorUserId) return null;
        return Results.Problem(
            detail: "Only this streamer competitor or an admin can manage their moderator delegations.",
            statusCode: StatusCodes.Status403Forbidden);
    }
}
