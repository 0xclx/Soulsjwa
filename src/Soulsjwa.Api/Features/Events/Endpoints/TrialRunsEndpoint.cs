using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record TrialRunResponse(
    Guid Id,
    Guid EventId,
    Guid EventGameId,
    Guid UserId,
    string State,
    DateTime? StartedAt,
    DateTime? EndedAt);

/// <summary>
/// Per-competitor trial/training runs. Scoped to
/// (event, game, competitor); works whether or not the game is the event's
/// active game. Enabling creates a <see cref="TrialRun"/> "slot"; disabling
/// deletes it outright, cascading away every completion/failure recorded
/// under it. Start/stop/reset mutate its <see cref="TrialRunState"/>.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="CompletedObjectivesEndpoint"/> for why.
/// </summary>
public class TrialRunsEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/events/{eventId:guid}/games/{eventGameId:guid}/trial-runs/{userId:guid}");

        group.MapGet("/", GetTrialRun)
            .WithName("GetTrialRun")
            .WithSummary("Gets a competitor's trial run state for a game, if enabled")
            .Produces<TrialRunResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/", EnableTrialRun)
            .WithName("EnableTrialRun")
            .WithSummary("Enables trial mode for a competitor+game (idempotent)")
            .Produces<TrialRunResponse>(StatusCodes.Status201Created)
            .Produces<TrialRunResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapDelete("/", DisableTrialRun)
            .WithName("DisableTrialRun")
            .WithSummary("Disables trial mode, hard-deleting this competitor's trial records for this event+game")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPost("/start", StartTrialRun)
            .WithName("StartTrialRun")
            .WithSummary("Starts (or resumes) a competitor's trial run")
            .Produces<TrialRunResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPost("/stop", StopTrialRun)
            .WithName("StopTrialRun")
            .WithSummary("Pauses a running trial run")
            .Produces<TrialRunResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPost("/reset", ResetTrialRun)
            .WithName("ResetTrialRun")
            .WithSummary("Resets a trial run to NotStarted, deleting its own recorded completions/failures")
            .Produces<TrialRunResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    internal static async Task<IResult> GetTrialRun(
        Guid eventId, Guid eventGameId, Guid userId, AppDbContext db, CancellationToken ct)
    {
        var trialRun = await db.TrialRuns
            .FirstOrDefaultAsync(t => t.EventId == eventId && t.EventGameId == eventGameId && t.UserId == userId, ct);
        return trialRun is null
            ? Results.Problem(detail: "Trial run not enabled.", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(MapToResponse(trialRun));
    }

    internal static async Task<IResult> EnableTrialRun(
        Guid eventId, Guid eventGameId, Guid userId,
        ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IOutputCacheStore cache, CancellationToken ct)
    {
        var (ev, eventGame, error) = await LoadContextAsync(eventId, eventGameId, principal, db, ct);
        if (error is not null) return error;

        // The three enable conditions, all refused with 403.
        if (ev!.IsArchived)
            return Forbid("Trial runs cannot be enabled on an archived event.");
        if (!ev.AllowTrialRuns)
            return Forbid("Trial runs are not allowed for this event.");

        var isCompetitor = await db.EventCompetitors.AnyAsync(ec => ec.EventId == eventId && ec.UserId == userId, ct);
        if (!isCompetitor)
            return Forbid("User is not a competitor in this event.");

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev, principal, userId, db, ct) is { } authError)
            return authError;

        // Creating the slot is what starts blocking official writes, so it must
        // serialize against them on the advisory lock the tick paths take —
        // otherwise a tick and an enable that each read a clean state commit
        // together into the mixed state the two rules forbid.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await ObjectiveOutcomeLock.AcquireAsync(db, eventGameId, ct);

        var existing = await db.TrialRuns
            .FirstOrDefaultAsync(t => t.EventGameId == eventGameId && t.UserId == userId, ct);
        if (existing is not null)
            return Results.Ok(MapToResponse(existing));

        // Official progress and a trial are mutually exclusive per
        // (game, competitor): a practice run must not sit beside, or be
        // mistaken for, a real attempt that has already begun.
        if (await TrialRunLookup.HasOfficialOutcomeAsync(db, eventGameId, userId, ct))
            return Results.Problem(
                detail: TrialRunLookup.OfficialProgressDetail,
                statusCode: StatusCodes.Status409Conflict);

        var trialRun = new TrialRun { EventId = eventId, EventGameId = eventGameId, UserId = userId };
        db.TrialRuns.Add(trialRun);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.TrialRunEnabled, callerId,
            eventId: eventId, eventGameId: eventGameId, subjectUserId: userId,
            after: new { trialRun.Id });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        // GameBreakdown.IsTrialActive is part of the scoreboard payload, so
        // every trial state transition has to drop the cached scoreboard —
        // otherwise the trial badge lags by the cache policy's expiry (1h).
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);

        return Results.Created(
            $"/api/v1/events/{eventId}/games/{eventGameId}/trial-runs/{userId}", MapToResponse(trialRun));
    }

    internal static async Task<IResult> DisableTrialRun(
        Guid eventId, Guid eventGameId, Guid userId,
        ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IOutputCacheStore cache, CancellationToken ct)
    {
        var (ev, _, error) = await LoadContextAsync(eventId, eventGameId, principal, db, ct);
        if (error is not null) return error;

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        var trialRun = await db.TrialRuns
            .Include(t => t.CompletedObjectives)
            .Include(t => t.FailedObjectives)
            .FirstOrDefaultAsync(t => t.EventGameId == eventGameId && t.UserId == userId, ct);
        if (trialRun is null)
            return Results.Problem(detail: "Trial run not enabled.", statusCode: StatusCodes.Status404NotFound);

        // Deleting the row cascades away every CompletedObjective/FailedObjective
        // referencing it — exactly this competitor's trial data for exactly this
        // game, nothing adjacent.
        var completedCount = trialRun.CompletedObjectives.Count;
        var failedCount = trialRun.FailedObjectives.Count;
        db.TrialRuns.Remove(trialRun);

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.TrialRunDisabled, callerId,
            eventId: eventId, eventGameId: eventGameId, subjectUserId: userId,
            before: new { trialRun.Id, CompletedRowsDeleted = completedCount, FailedRowsDeleted = failedCount });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);

        return Results.NoContent();
    }

    internal static async Task<IResult> StartTrialRun(
        Guid eventId, Guid eventGameId, Guid userId,
        ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IOutputCacheStore cache, CancellationToken ct)
    {
        var (ev, _, error) = await LoadContextAsync(eventId, eventGameId, principal, db, ct);
        if (error is not null) return error;

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        // Re-check the owner's conditions rather than trusting that they held
        // when the slot was created: a slot can outlive the AllowTrialRuns
        // switch, and a trial is playable on a game the owner has disabled.
        //
        // This guards the transition only. A run already Running when the owner
        // revokes the switch keeps recording and publishing: revoking stops new
        // trials, it is not a kill switch — deliberately, so it neither destroys
        // practice data nor strands a competitor on a game blocked for official
        // writes.
        if (ev!.IsArchived)
            return Forbid("Trial runs cannot be started on an archived event.");
        if (!ev.AllowTrialRuns)
            return Forbid("Trial runs are not allowed for this event.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await ObjectiveOutcomeLock.AcquireAsync(db, eventGameId, ct);

        var trialRun = await db.TrialRuns
            .FirstOrDefaultAsync(t => t.EventGameId == eventGameId && t.UserId == userId, ct);
        if (trialRun is null)
            return Results.Problem(detail: "Trial run not enabled.", statusCode: StatusCodes.Status404NotFound);

        if (trialRun.State == TrialRunState.Running)
            return Results.Ok(MapToResponse(trialRun));
        if (trialRun.State == TrialRunState.Completed)
            return Results.Problem(
                detail: "This trial run has ended; reset it to start again.",
                statusCode: StatusCodes.Status409Conflict);

        // Re-checked here and not only on enable: a slot can predate this rule
        // (or predate official rows that arrived while it was dormant on an
        // older build), and starting it is the moment it would begin putting
        // figures on the public scoreboard next to a real attempt.
        if (await TrialRunLookup.HasOfficialOutcomeAsync(db, eventGameId, userId, ct))
            return Results.Problem(
                detail: TrialRunLookup.OfficialProgressDetail,
                statusCode: StatusCodes.Status409Conflict);

        var before = new { trialRun.State };
        trialRun.State = TrialRunState.Running;
        trialRun.StartedAt ??= DateTime.UtcNow;
        trialRun.EndedAt = null;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.TrialRunStarted, callerId,
            eventId: eventId, eventGameId: eventGameId, subjectUserId: userId,
            before: before, after: new { trialRun.State, trialRun.StartedAt });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);

        return Results.Ok(MapToResponse(trialRun));
    }

    internal static async Task<IResult> StopTrialRun(
        Guid eventId, Guid eventGameId, Guid userId,
        ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IOutputCacheStore cache, CancellationToken ct)
    {
        var (ev, _, error) = await LoadContextAsync(eventId, eventGameId, principal, db, ct);
        if (error is not null) return error;

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        var trialRun = await db.TrialRuns
            .FirstOrDefaultAsync(t => t.EventGameId == eventGameId && t.UserId == userId, ct);
        if (trialRun is null)
            return Results.Problem(detail: "Trial run not enabled.", statusCode: StatusCodes.Status404NotFound);

        if (trialRun.State != TrialRunState.Running)
            return Results.Problem(
                detail: "Only a running trial run can be stopped.",
                statusCode: StatusCodes.Status409Conflict);

        var before = new { trialRun.State };
        trialRun.State = TrialRunState.Paused;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.TrialRunStopped, callerId,
            eventId: eventId, eventGameId: eventGameId, subjectUserId: userId,
            before: before, after: new { trialRun.State });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);

        return Results.Ok(MapToResponse(trialRun));
    }

    internal static async Task<IResult> ResetTrialRun(
        Guid eventId, Guid eventGameId, Guid userId,
        ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IOutputCacheStore cache, CancellationToken ct)
    {
        var (ev, _, error) = await LoadContextAsync(eventId, eventGameId, principal, db, ct);
        if (error is not null) return error;

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        var trialRun = await db.TrialRuns
            .FirstOrDefaultAsync(t => t.EventGameId == eventGameId && t.UserId == userId, ct);
        if (trialRun is null)
            return Results.Problem(detail: "Trial run not enabled.", statusCode: StatusCodes.Status404NotFound);

        // Two deletes and a state change must land together, under the advisory
        // lock the tick paths take. Without the transaction, a failure after the
        // deletes leaves the rows gone but the run still Running; without the
        // lock, a completion that resolved this run a moment earlier can insert
        // a row *after* the delete, leaving trial rows on a NotStarted run —
        // invisible on the scoreboard but counted by the Trial tab.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await ObjectiveOutcomeLock.AcquireAsync(db, eventGameId, ct);

        // Deletes this run's trial rows directly rather than by cascade: the run
        // row itself is kept, so the competitor need not re-enable.
        var deletedCompleted = await db.CompletedObjectives
            .Where(co => co.TrialRunId == trialRun.Id)
            .ExecuteDeleteAsync(ct);
        var deletedFailed = await db.FailedObjectives
            .Where(f => f.TrialRunId == trialRun.Id)
            .ExecuteDeleteAsync(ct);

        var before = new { trialRun.State, trialRun.StartedAt, trialRun.EndedAt };
        trialRun.State = TrialRunState.NotStarted;
        trialRun.StartedAt = null;
        trialRun.EndedAt = null;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.TrialRunReset, callerId,
            eventId: eventId, eventGameId: eventGameId, subjectUserId: userId,
            before: before,
            after: new { trialRun.State, CompletedRowsDeleted = deletedCompleted, FailedRowsDeleted = deletedFailed });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);

        return Results.Ok(MapToResponse(trialRun));
    }

    private static async Task<(Event? Event, EventGame? EventGame, IResult? Error)> LoadContextAsync(
        Guid eventId, Guid eventGameId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        // Bypass the archived-event query filter: whether the event is
        // archived is itself one of the conditions this module checks and
        // reports as 403 — but only to the event's members. To anyone else an
        // archived event is indistinguishable from a missing one (404), as on
        // every other route, so this cannot be used to confirm it exists.
        var ev = await db.Events.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null || (ev.IsArchived && !await EventOwnership.IsEventMemberAsync(ev, principal, db, ct)))
            return (null, null, Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound));

        var eventGame = await db.EventGames.FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);
        if (eventGame is null)
            return (ev, null, Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound));

        return (ev, eventGame, null);
    }

    private static IResult Forbid(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden);

    private static TrialRunResponse MapToResponse(TrialRun trialRun) => new(
        trialRun.Id,
        trialRun.EventId,
        trialRun.EventGameId,
        trialRun.UserId,
        trialRun.State.ToString(),
        trialRun.StartedAt,
        trialRun.EndedAt);
}
