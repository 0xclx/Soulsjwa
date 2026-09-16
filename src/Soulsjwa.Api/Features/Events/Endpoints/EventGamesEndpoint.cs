using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record AddGameToEventRequest(int GameId, string? Name = null, string? Description = null);
public sealed record AddCustomGameToEventRequest(string Name, string? Description);
public sealed record ReorderEventGamesRequest(List<Guid> EventGameIds);
public sealed record ReorderObjectivesRequest(List<Guid> ObjectiveIds);
public sealed record PatchEventGameRequest(string? Name, string? Description);

/// <summary>
/// An event's games: adding from the catalogue or as a custom entry, removing,
/// renaming, reordering, and the "exactly one enabled game" switch.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="CompletedObjectivesEndpoint"/> for why.
/// </summary>
public class EventGamesEndpoint : IEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 1000;
    private const string ActiveGameIndexName = "IX_EventGames_EventId_ActiveGame";

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/events/{eventId:guid}/games");

        group.MapPost("/", AddGame)
            .WithName("AddGameToEvent")
            .WithSummary("Adds a predefined game to an event with an optional custom display name (owner only). The same game can be added multiple times with different names.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPost("/custom", AddCustomGame)
            .WithName("AddCustomGameToEvent")
            .WithSummary("Adds a custom (per-event) game to an event (owner only)")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapDelete("/{eventGameId:guid}", RemoveEventGame)
            .WithName("RemoveEventGame")
            .WithSummary("Removes an event game by its ID (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPost("/{eventGameId:guid}/enable", EnableEventGame)
            .WithName("EnableEventGame")
            .WithSummary("Sets a game as the event's single active game (owner only); every other game is disabled in the same transaction")
            .Produces<List<EventGameResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPost("/{eventGameId:guid}/disable", DisableEventGame)
            .WithName("DisableEventGame")
            .WithSummary("Disables a game within an event, preventing objective completions for this game (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPatch("/{eventGameId:guid}", PatchEventGame)
            .WithName("PatchEventGame")
            .WithSummary("Updates an event game's display name/description (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPut("/reorder", ReorderEventGames)
            .WithName("ReorderEventGames")
            .WithSummary("Sets the display order of games within an event (owner only). The request must contain exactly the event's current game IDs.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPut("/{eventGameId:guid}/objectives/reorder", ReorderObjectives)
            .WithName("ReorderEventGameObjectives")
            .WithSummary("Sets the display order of objectives within a game (owner only). The request must contain exactly the event game's current objective IDs.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .RequireAuthorization();
    }

    internal static async Task<IResult> AddGame(
        Guid eventId,
        AddGameToEventRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGamesEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "add games", db, ct);
        if (error is not null) return error;

        if (ev!.IsStarted)
            return Results.Problem(detail: "Cannot add games while the event is running.", statusCode: StatusCodes.Status409Conflict);

        var gameExists = await db.Games.AnyAsync(g => g.Id == request.GameId, ct);
        if (!gameExists)
            return Results.Problem(detail: "Game not found.", statusCode: StatusCodes.Status404NotFound);

        var errors = new Dictionary<string, string[]>();
        if (request.Name is not null && request.Name.Length > MaxNameLength)
            errors["Name"] = [$"Name must be {MaxNameLength} characters or fewer."];
        if (request.Description?.Length > MaxDescriptionLength)
            errors["Description"] = [$"Description must be {MaxDescriptionLength} characters or fewer."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var nextSortOrder = (await db.EventGames
            .Where(eg => eg.EventId == eventId)
            .Select(eg => (int?)eg.SortOrder)
            .MaxAsync(ct) ?? -1) + 1;

        var eventGame = new EventGame
        {
            EventId = eventId,
            KnownGameId = request.GameId,
            CustomGameName = request.Name?.Trim(),
            CustomGameDescription = request.Description?.Trim(),
            SortOrder = nextSortOrder
        };

        db.EventGames.Add(eventGame);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventGameAdded, callerId, eventId: eventId, eventGameId: eventGame.Id,
            after: new
            {
                eventGame.KnownGameId,
                Name = eventGame.CustomGameName,
                Description = eventGame.CustomGameDescription,
                eventGame.IsCustomGame,
                eventGame.IsEnabled,
            });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.EventGameAdded(request.GameId, eventGame.Id, eventId, callerId);
        return Results.Created($"/api/v1/events/{eventId}/games/{eventGame.Id}", new { eventGame.Id });
    }

    internal static async Task<IResult> AddCustomGame(
        Guid eventId,
        AddCustomGameToEventRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGamesEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "add custom games", db, ct);
        if (error is not null) return error;

        if (ev!.IsStarted)
            return Results.Problem(detail: "Cannot add games while the event is running.", statusCode: StatusCodes.Status409Conflict);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
            errors["Name"] = ["Name is required."];
        else if (request.Name.Length > MaxNameLength)
            errors["Name"] = [$"Name must be {MaxNameLength} characters or fewer."];

        if (request.Description?.Length > MaxDescriptionLength)
            errors["Description"] = [$"Description must be {MaxDescriptionLength} characters or fewer."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var nextSortOrder = (await db.EventGames
            .Where(eg => eg.EventId == eventId)
            .Select(eg => (int?)eg.SortOrder)
            .MaxAsync(ct) ?? -1) + 1;

        var eventGame = new EventGame
        {
            EventId = eventId,
            CustomGameName = request.Name.Trim(),
            CustomGameDescription = request.Description?.Trim(),
            SortOrder = nextSortOrder
        };

        db.EventGames.Add(eventGame);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventGameAdded, callerId, eventId: eventId, eventGameId: eventGame.Id,
            after: new
            {
                Name = eventGame.CustomGameName,
                Description = eventGame.CustomGameDescription,
                IsCustomGame = true,
                eventGame.IsEnabled,
            });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.CustomEventGameAdded(eventGame.Id, eventId, callerId);
        return Results.Created($"/api/v1/events/{eventId}/games/{eventGame.Id}", new { eventGame.Id });
    }

    internal static async Task<IResult> RemoveEventGame(
        Guid eventId,
        Guid eventGameId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGamesEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "remove games", db, ct);
        if (error is not null) return error;

        if (ev!.IsStarted)
            return Results.Problem(detail: "Cannot remove games while the event is running.", statusCode: StatusCodes.Status409Conflict);

        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);

        if (eventGame is null)
            return Results.Problem(detail: "Game not found in this event.", statusCode: StatusCodes.Status404NotFound);

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventGameRemoved, callerId, eventId: eventId, eventGameId: eventGameId,
            before: new
            {
                eventGame.KnownGameId,
                Name = eventGame.CustomGameName,
                Description = eventGame.CustomGameDescription,
                eventGame.IsCustomGame,
                eventGame.IsEnabled,
            });
        db.EventGames.Remove(eventGame);
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.EventGameRemoved(eventGameId, eventId, callerId);
        return Results.NoContent();
    }

    internal static async Task<IResult> EnableEventGame(
        Guid eventId,
        Guid eventGameId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGamesEndpoint> logger,
        CancellationToken ct)
    {
        // Enabling is "set active", not "add to set": at most one game is
        // enabled per event, so every other enabled game is disabled in the
        // same SaveChanges — see the partial unique index on
        // (EventId) WHERE "IsEnabled" for the DB-level guarantee of this.
        var ev = await db.Events
            .Include(e => e.Competitors).ThenInclude(c => c.User)
            .Include(e => e.Competitors).ThenInclude(c => c.Moderators).ThenInclude(m => m.Moderator)
            .Include(e => e.EventGames).ThenInclude(eg => eg.KnownGame)
            .Include(e => e.EventGames).ThenInclude(eg => eg.Objectives)
            // Same graph as GetEvent, same reason for split queries: one
            // joined query multiplies rows by competitors × games × objectives.
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(ev, principal, "enable games") is { } ownerError)
            return ownerError;

        if (!ev.IsStarted)
            return Results.Problem(detail: "A game can only be enabled while the event is running.", statusCode: StatusCodes.Status409Conflict);

        var eventGame = ev.EventGames.FirstOrDefault(eg => eg.Id == eventGameId);
        if (eventGame is null)
            return Results.Problem(detail: "Game not found in this event.", statusCode: StatusCodes.Status404NotFound);

        var callerId = EventOwnership.GetUserId(principal);

        if (!eventGame.IsEnabled)
        {
            var previouslyActive = ev.EventGames.Where(eg => eg.IsEnabled && eg.Id != eventGameId).ToList();
            var previouslyActiveIds = previouslyActive.Select(g => g.Id).ToList();

            // IX_EventGames_EventId_ActiveGame is a non-deferrable partial
            // unique index — Postgres checks it per statement, not at commit,
            // so the clear must run (and be committed as having happened)
            // before the set, not rely on EF's undocumented statement
            // ordering within one SaveChanges. ExecuteUpdateAsync makes that
            // ordering explicit.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            await db.EventGames
                .Where(eg => eg.EventId == eventId && eg.IsEnabled && eg.Id != eventGameId)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsEnabled, false), ct);

            // ExecuteUpdateAsync bypasses the change tracker, so mirror the
            // clear onto the tracked entities too — the response below is built
            // from ev.EventGames. Detach each one *before* mutating it:
            // ExecuteUpdateAsync already persisted this change and advanced the
            // row's xmin, so a tracked-and-Modified entity would be UPDATEd
            // again by SaveChangesAsync with the stale xmin, and optimistic
            // concurrency would throw DbUpdateConcurrencyException over 0 rows
            // affected even though the database is already correct. Detaching
            // keeps the plain C# reference usable for ev.EventGames.
            foreach (var other in previouslyActive)
            {
                db.Entry(other).State = EntityState.Detached;
                other.IsEnabled = false;
            }

            eventGame.IsEnabled = true;
            audit.Log(db, AuditEventTypes.EventGameEnabled, callerId, eventId: eventId, eventGameId: eventGameId,
                before: new { IsEnabled = false, PreviouslyActiveEventGameIds = previouslyActiveIds },
                after: new { IsEnabled = true });

            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: ActiveGameIndexName
                })
            {
                return Results.Problem(
                    detail: "Another game was enabled concurrently. Retry.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
            logger.EventGameEnabled(eventGameId, eventId, callerId);
        }

        return Results.Ok(EventsEndpoint.MapToResponse(ev).Games);
    }

    internal static async Task<IResult> DisableEventGame(
        Guid eventId,
        Guid eventGameId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGamesEndpoint> logger,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "disable games", db, ct);
        if (error is not null) return error;

        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);
        if (eventGame is null)
            return Results.Problem(detail: "Game not found in this event.", statusCode: StatusCodes.Status404NotFound);

        if (!eventGame.IsEnabled) return Results.NoContent();

        eventGame.IsEnabled = false;
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventGameDisabled, callerId, eventId: eventId, eventGameId: eventGameId,
            before: new { IsEnabled = true }, after: new { IsEnabled = false });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.EventGameDisabled(eventGameId, eventId, callerId);
        return Results.NoContent();
    }

    internal static async Task<IResult> PatchEventGame(
        Guid eventId,
        Guid eventGameId,
        PatchEventGameRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGamesEndpoint> logger,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "update games", db, ct);
        if (error is not null) return error;

        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);
        if (eventGame is null)
            return Results.Problem(detail: "Game not found in this event.", statusCode: StatusCodes.Status404NotFound);

        var before = new { eventGame.CustomGameName, eventGame.CustomGameDescription };

        if (request.Name is not null)
        {
            var trimmedName = request.Name.Trim();
            if (trimmedName.Length == 0)
            {
                if (eventGame.IsCustomGame)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["Name"] = ["Name is required for custom games."]
                    });
                eventGame.CustomGameName = null;
            }
            else
            {
                if (trimmedName.Length > MaxNameLength)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["Name"] = [$"Name must be {MaxNameLength} characters or fewer."]
                    });
                eventGame.CustomGameName = trimmedName;
            }
        }

        if (request.Description is not null)
        {
            var trimmedDescription = request.Description.Trim();
            if (trimmedDescription.Length == 0)
            {
                eventGame.CustomGameDescription = null;
            }
            else
            {
                if (trimmedDescription.Length > MaxDescriptionLength)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["Description"] = [$"Description must be {MaxDescriptionLength} characters or fewer."]
                    });
                eventGame.CustomGameDescription = trimmedDescription;
            }
        }

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventGameUpdated, callerId, eventId: eventId, eventGameId: eventGameId,
            before: before,
            after: new { eventGame.CustomGameName, eventGame.CustomGameDescription });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                detail: "This record was modified by someone else. Reload and reapply your change.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.EventGameUpdated(eventGameId, eventId, callerId);

        return Results.NoContent();
    }

    internal static async Task<IResult> ReorderEventGames(
        Guid eventId,
        ReorderEventGamesRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGamesEndpoint> logger,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "reorder games", db, ct);
        if (error is not null) return error;

        var eventGames = await db.EventGames
            .Where(eg => eg.EventId == eventId)
            .ToListAsync(ct);

        var currentIds = eventGames.Select(eg => eg.Id).ToHashSet();
        var requestedIds = request.EventGameIds;
        if (requestedIds.Count != currentIds.Count
            || requestedIds.Distinct().Count() != requestedIds.Count
            || !requestedIds.All(currentIds.Contains))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["EventGameIds"] = ["Must contain exactly the event's current game IDs, with no duplicates."]
            });
        }

        var eventGamesById = eventGames.ToDictionary(eg => eg.Id);
        for (var index = 0; index < requestedIds.Count; index++)
            eventGamesById[requestedIds[index]].SortOrder = index;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventGamesReordered, callerId, eventId: eventId,
            after: new { EventGameIds = requestedIds });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.EventGamesReordered(eventId, callerId);

        return Results.NoContent();
    }

    internal static async Task<IResult> ReorderObjectives(
        Guid eventId,
        Guid eventGameId,
        ReorderObjectivesRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGamesEndpoint> logger,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "reorder objectives", db, ct);
        if (error is not null) return error;

        var eventGameExists = await db.EventGames.AnyAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);
        if (!eventGameExists)
            return Results.Problem(detail: "Game not found in this event.", statusCode: StatusCodes.Status404NotFound);

        var objectives = await db.Objectives
            .Where(o => o.EventGameId == eventGameId)
            .ToListAsync(ct);

        var currentIds = objectives.Select(o => o.Id).ToHashSet();
        var requestedIds = request.ObjectiveIds;
        if (requestedIds.Count != currentIds.Count
            || requestedIds.Distinct().Count() != requestedIds.Count
            || !requestedIds.All(currentIds.Contains))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ObjectiveIds"] = ["Must contain exactly the event game's current objective IDs, with no duplicates."]
            });
        }

        var objectivesById = objectives.ToDictionary(o => o.Id);
        for (var index = 0; index < requestedIds.Count; index++)
            objectivesById[requestedIds[index]].SortOrder = index;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.ObjectivesReordered, callerId, eventId: eventId, eventGameId: eventGameId,
            after: new { ObjectiveIds = requestedIds });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectivesReordered(eventGameId, eventId, callerId);

        return Results.NoContent();
    }
}
