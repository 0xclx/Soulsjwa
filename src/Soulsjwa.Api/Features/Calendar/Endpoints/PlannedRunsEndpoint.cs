using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Calendar.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Calendar.Endpoints;

public sealed record PlannedRunResponse(
    Guid Id,
    Guid EventId,
    Guid EventGameId,
    Guid UserId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Color,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreatePlannedRunRequest(Guid EventGameId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Color = null);
public sealed record UpdatePlannedRunRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Color = null);

/// <summary>
/// CRUD for a competitor's scheduled play sessions. Write access
/// mirrors <see cref="EventOwnership.RequireCanEditCompetitorInfoAsync"/>: the
/// competitor themselves, the event owner, an admin, or one of that
/// competitor's delegated moderators. Readable by everyone.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class PlannedRunsEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(
            ApiRoutes.Prefix + "/events/{eventId:guid}/competitors/{userId:guid}/planned-runs");

        group.MapGet("/", List)
            .WithName("ListPlannedRuns")
            .WithSummary("Lists a competitor's planned runs for an event")
            .Produces<List<PlannedRunResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/", Create)
            .WithName("CreatePlannedRun")
            .WithSummary("Adds a planned run for a competitor")
            .Produces<PlannedRunResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPut("/{plannedRunId:guid}", Update)
            .WithName("UpdatePlannedRun")
            .WithSummary("Updates a planned run's schedule")
            .Produces<PlannedRunResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapDelete("/{plannedRunId:guid}", Delete)
            .WithName("DeletePlannedRun")
            .WithSummary("Removes a planned run")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    internal static async Task<IResult> List(
        Guid eventId, Guid userId, AppDbContext db, CancellationToken ct)
    {
        var eventExists = await db.Events.AnyAsync(e => e.Id == eventId, ct);
        if (!eventExists)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        var runs = await db.PlannedRuns
            .Where(r => r.EventId == eventId && r.UserId == userId)
            .OrderBy(r => r.StartsAt)
            .Select(r => ToResponse(r))
            .ToListAsync(ct);

        return Results.Ok(runs);
    }

    internal static async Task<IResult> Create(
        Guid eventId,
        Guid userId,
        CreatePlannedRunRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.Id == request.EventGameId && eg.EventId == eventId, ct);
        if (eventGame is null)
            return Results.Problem(detail: "Event game not found.", statusCode: StatusCodes.Status404NotFound);

        var isCompetitor = await db.EventCompetitors
            .AnyAsync(ec => ec.EventId == eventId && ec.UserId == userId, ct);
        if (!isCompetitor)
            return Results.Problem(detail: "User is not a competitor in this event.", statusCode: StatusCodes.Status404NotFound);

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        var (color, errors) = ValidateColor(request.Color);
        if (request.EndsAt <= request.StartsAt)
            errors["endsAt"] = ["EndsAt must be after StartsAt."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var now = DateTime.UtcNow;
        var run = new PlannedRun
        {
            EventId = eventId,
            EventGameId = request.EventGameId,
            UserId = userId,
            StartsAt = UtcTime.ToStorage(request.StartsAt),
            EndsAt = UtcTime.ToStorage(request.EndsAt),
            Color = color,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.PlannedRuns.Add(run);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.PlannedRunCreated, callerId,
            eventId: eventId, eventGameId: request.EventGameId, subjectUserId: userId,
            after: new { run.Id, run.StartsAt, run.EndsAt });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Calendar, ct);

        return Results.Created(
            $"/api/v1/events/{eventId}/competitors/{userId}/planned-runs/{run.Id}",
            ToResponse(run));
    }

    internal static async Task<IResult> Update(
        Guid eventId,
        Guid userId,
        Guid plannedRunId,
        UpdatePlannedRunRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var run = await db.PlannedRuns
            .FirstOrDefaultAsync(r => r.Id == plannedRunId && r.EventId == eventId && r.UserId == userId, ct);
        if (run is null)
            return Results.Problem(detail: "Planned run not found.", statusCode: StatusCodes.Status404NotFound);

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        var (color, errors) = ValidateColor(request.Color);
        if (request.EndsAt <= request.StartsAt)
            errors["endsAt"] = ["EndsAt must be after StartsAt."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var before = new { run.StartsAt, run.EndsAt, run.Color };
        run.StartsAt = UtcTime.ToStorage(request.StartsAt);
        run.EndsAt = UtcTime.ToStorage(request.EndsAt);
        run.Color = color;
        run.UpdatedAt = DateTime.UtcNow;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.PlannedRunUpdated, callerId,
            eventId: eventId, eventGameId: run.EventGameId, subjectUserId: userId,
            before: before, after: new { run.StartsAt, run.EndsAt, run.Color });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Calendar, ct);

        return Results.Ok(ToResponse(run));
    }

    internal static async Task<IResult> Delete(
        Guid eventId,
        Guid userId,
        Guid plannedRunId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var run = await db.PlannedRuns
            .FirstOrDefaultAsync(r => r.Id == plannedRunId && r.EventId == eventId && r.UserId == userId, ct);
        if (run is null)
            return Results.Problem(detail: "Planned run not found.", statusCode: StatusCodes.Status404NotFound);

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        db.PlannedRuns.Remove(run);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.PlannedRunDeleted, callerId,
            eventId: eventId, eventGameId: run.EventGameId, subjectUserId: userId,
            before: new { run.Id, run.StartsAt, run.EndsAt });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Calendar, ct);

        return Results.NoContent();
    }

    /// <summary>
    /// Colour is optional on the wire: omitted or null yields
    /// <see cref="CalendarEntryColor.Default"/>. Only a value that is present but
    /// unrecognized is a validation error.
    /// </summary>
    private static (CalendarEntryColor Color, Dictionary<string, string[]> Errors) ValidateColor(string? colorRaw)
    {
        var errors = new Dictionary<string, string[]>();
        if (colorRaw is null)
            return (CalendarEntryColor.Default, errors);

        if (!Enum.TryParse(colorRaw, ignoreCase: false, out CalendarEntryColor color) || !Enum.IsDefined(color))
            errors["color"] = [$"Color must be one of: {string.Join(", ", Enum.GetNames<CalendarEntryColor>())}."];
        return (color, errors);
    }

    private static PlannedRunResponse ToResponse(PlannedRun r) => new(
        r.Id, r.EventId, r.EventGameId, r.UserId, r.StartsAt, r.EndsAt, r.Color.ToString(), r.CreatedAt, r.UpdatedAt);
}
