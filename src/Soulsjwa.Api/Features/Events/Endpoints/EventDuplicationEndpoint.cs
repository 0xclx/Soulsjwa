using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Events.Endpoints;

/// <summary>
/// Duplicates an event's configuration — games and objectives only (SPEC
/// §7.12). Never copies competitors, moderators, completions, failures,
/// competitor infos, overlay tokens, rules, calendar entries, planned runs,
/// or audit history. The copy starts fresh: not started, not archived, not
/// featured, no URL alias (aliases are unique), and no game enabled — an
/// enabled game on a not-yet-started event is a state no other code path
/// can reach (EnableEventGame requires IsStarted), so the copy must not
/// either.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class EventDuplicationEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Prefix + "/events/{id:guid}/duplicate", DuplicateEvent)
            .WithName("DuplicateEvent")
            .WithSummary("Duplicates an event's games and objectives, without competitors or history (admin only)")
            .Produces<EventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAdmin();
    }

    internal static async Task<IResult> DuplicateEvent(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        CancellationToken ct)
    {
        // Matches event creation, which is admin-only (EventsEndpoint.CreateEvent).
        if (!EventOwnership.IsAdmin(principal))
            return AdminAccess.Forbid();

        // Bypass the global IsArchived query filter: duplicating an archived
        // source event must work, and the source itself is left
        // untouched either way.
        var source = await db.Events
            .IgnoreQueryFilters()
            .Include(e => e.EventGames).ThenInclude(eg => eg.Objectives)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (source is null)
            return Results.Problem(
                detail: "Event not found.",
                statusCode: StatusCodes.Status404NotFound);

        var callerId = EventOwnership.GetUserId(principal);

        var copy = new Event
        {
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            TieBreakMode = source.TieBreakMode,
            CreatedById = callerId,
        };

        foreach (var game in source.EventGames.OrderBy(g => g.SortOrder))
        {
            var gameCopy = new EventGame
            {
                KnownGameId = game.KnownGameId,
                CustomGameName = game.CustomGameName,
                CustomGameDescription = game.CustomGameDescription,
                IsEnabled = false,
                SortOrder = game.SortOrder,
            };

            foreach (var objective in game.Objectives.OrderBy(o => o.SortOrder))
            {
                gameCopy.Objectives.Add(new Objective
                {
                    GameId = objective.GameId,
                    Name = objective.Name,
                    Score = objective.Score,
                    Category = objective.Category,
                    Metadata = objective.Metadata,
                    Rule = objective.Rule,
                    FailRule = objective.FailRule,
                    IsPredefined = objective.IsPredefined,
                    SortOrder = objective.SortOrder,
                });
            }

            copy.EventGames.Add(gameCopy);
        }

        db.Events.Add(copy);
        audit.Log(db, AuditEventTypes.EventDuplicated, callerId, eventId: copy.Id,
            before: new { SourceEventId = source.Id, SourceEventName = source.Name },
            after: new { copy.Id, copy.Name });
        await db.SaveChangesAsync(ct);

        // Re-fetch with the same includes GetEvent uses so KnownGame names
        // resolve correctly in the response, rather than mapping the
        // in-memory graph (whose KnownGame navigations were never loaded).
        var persisted = await db.Events
            .Include(e => e.Competitors).ThenInclude(c => c.User)
            .Include(e => e.Competitors).ThenInclude(c => c.Moderators).ThenInclude(m => m.Moderator)
            .Include(e => e.EventGames).ThenInclude(eg => eg.KnownGame)
            .Include(e => e.EventGames).ThenInclude(eg => eg.Objectives)
            .AsSplitQuery()
            .FirstAsync(e => e.Id == copy.Id, ct);

        return Results.Created($"/api/v1/events/{copy.Id}", EventsEndpoint.MapToResponse(persisted));
    }
}
