using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events.Endpoints;

/// <summary>
/// Manual failure and reset of an objective for a competitor. Mirrors
/// <see cref="CompletedObjectivesEndpoint"/>'s completion endpoints exactly:
/// same self/on-behalf-of permission model, same event-started/game-enabled
/// preconditions, same scoreboard cache eviction. An objective is never both
/// completed and failed — marking it one while it is already the other is a
/// 409. Resetting deletes the <see cref="FailedObjective"/> row (there is no
/// "manual reset" flag), so the objective becomes pending again.
///
/// <see cref="FailRemainingObjectives"/> is the game-wide form for a run that
/// is over (a death in a no-death run): every objective of the game still
/// pending for the competitor fails in one transaction, so their outcome for
/// the game is terminal and the scoreboards show them finished. Objectives
/// already completed or failed are left alone rather than conflicting.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="CompletedObjectivesEndpoint"/>.
/// </summary>
/// <summary>
/// What <see cref="FailedObjectivesEndpoint.FailRemainingObjectives"/> did:
/// how many objectives it failed, how many it left alone because they were
/// already completed or already failed, and the game's total.
/// </summary>
public sealed record FailRemainingObjectivesResponse(
    int FailedCount,
    int AlreadyCompletedCount,
    int AlreadyFailedCount,
    int TotalObjectives);

public class FailedObjectivesEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.EventById);

        group.MapPost("/games/{eventGameId:guid}/objectives/{objectiveId:guid}/fail", FailObjective)
            .WithName("FailObjective")
            .WithSummary("Manually marks an objective failed for the current user (must be a competitor)")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPost("/games/{eventGameId:guid}/objectives/fail-remaining", FailRemainingObjectives)
            .WithName("FailRemainingObjectives")
            .WithSummary("Marks every still-pending objective of the game failed for the current user (must be a competitor)")
            .Produces<FailRemainingObjectivesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapDelete("/games/{eventGameId:guid}/objectives/{objectiveId:guid}/fail", ResetFailedObjective)
            .WithName("ResetFailedObjective")
            .WithSummary("Resets (unfails) an objective for the current user, returning it to pending")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();
    }

    internal static async Task<IResult> FailObjective(
        Guid eventId,
        Guid eventGameId,
        Guid objectiveId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<FailedObjectivesEndpoint> logger,
        CancellationToken ct,
        Guid? onBehalfOfUserId = null,
        Guid? expectedTrialRunId = null)
    {
        var (scope, error) = await ObjectiveOutcomeWrite.BeginAsync(
            eventId, eventGameId, objectiveId, principal, onBehalfOfUserId, expectedTrialRunId, db, ct);
        if (error is not null) return error;
        await using var write = scope!;
        var callerId = write.CallerId;
        var targetUserId = write.TargetUserId;
        var trialRunId = write.TrialRunId;

        var alreadyCompleted = await db.CompletedObjectives
            .AnyAsync(co => co.ObjectiveId == objectiveId && co.UserId == targetUserId && co.TrialRunId == trialRunId, ct);
        if (alreadyCompleted)
            return Results.Problem(
                detail: "Objective is already completed. Uncomplete it before marking it failed.",
                statusCode: StatusCodes.Status409Conflict);

        var alreadyFailed = await db.FailedObjectives
            .AnyAsync(f => f.ObjectiveId == objectiveId && f.UserId == targetUserId && f.TrialRunId == trialRunId, ct);
        if (alreadyFailed)
            return Results.Problem(
                detail: "Objective already failed.",
                statusCode: StatusCodes.Status409Conflict);

        db.FailedObjectives.Add(new FailedObjective
        {
            ObjectiveId = objectiveId,
            UserId = targetUserId,
            TrialRunId = trialRunId,
        });

        audit.Log(db, AuditEventTypes.ObjectiveFailed, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objectiveId,
            subjectUserId: targetUserId,
            after: new { FailedAt = DateTime.UtcNow, TrialRunId = trialRunId });

        await db.SaveChangesAsync(ct);
        await write.CommitAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectiveFailed(objectiveId, targetUserId, eventId, callerId);
        return Results.Created();
    }

    internal static async Task<IResult> FailRemainingObjectives(
        Guid eventId,
        Guid eventGameId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<FailedObjectivesEndpoint> logger,
        CancellationToken ct,
        Guid? onBehalfOfUserId = null,
        Guid? expectedTrialRunId = null)
    {
        // Same preamble as a single fail, minus the objective: the game, the
        // target, the lock and the official-or-trial record are resolved once
        // for the whole batch.
        var (scope, error) = await ObjectiveOutcomeWrite.BeginAsync(
            eventId, eventGameId, objectiveId: null, principal, onBehalfOfUserId, expectedTrialRunId, db, ct);
        if (error is not null) return error;
        await using var write = scope!;
        var callerId = write.CallerId;
        var targetUserId = write.TargetUserId;
        var trialRunId = write.TrialRunId;

        var objectiveIds = await db.Objectives
            .Where(o => o.EventGameId == eventGameId)
            .Select(o => o.Id)
            .ToListAsync(ct);
        var completedIds = await db.CompletedObjectives
            .Where(co => co.UserId == targetUserId && co.TrialRunId == trialRunId && co.Objective.EventGameId == eventGameId)
            .Select(co => co.ObjectiveId)
            .ToListAsync(ct);
        var failedIds = await db.FailedObjectives
            .Where(f => f.UserId == targetUserId && f.TrialRunId == trialRunId && f.Objective.EventGameId == eventGameId)
            .Select(f => f.ObjectiveId)
            .ToListAsync(ct);

        var resolved = completedIds.Concat(failedIds).ToHashSet();
        var remaining = objectiveIds.Where(id => !resolved.Contains(id)).ToList();
        var failedAt = DateTime.UtcNow;
        foreach (var objectiveId in remaining)
        {
            db.FailedObjectives.Add(new FailedObjective
            {
                ObjectiveId = objectiveId,
                UserId = targetUserId,
                TrialRunId = trialRunId,
                FailedAt = failedAt,
            });
        }

        // One audit row for the batch, as the trial-run reset does, rather
        // than one per objective: a game can carry hundreds.
        audit.Log(db, AuditEventTypes.ObjectivesRemainingFailed, callerId,
            eventId: eventId, eventGameId: eventGameId,
            subjectUserId: targetUserId,
            after: new
            {
                FailedAt = failedAt,
                TrialRunId = trialRunId,
                FailedCount = remaining.Count,
                ObjectiveIds = remaining,
            });

        await db.SaveChangesAsync(ct);
        await write.CommitAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectivesRemainingFailed(remaining.Count, eventGameId, targetUserId, eventId, callerId);
        return Results.Ok(new FailRemainingObjectivesResponse(
            remaining.Count, completedIds.Count, failedIds.Count, objectiveIds.Count));
    }

    internal static async Task<IResult> ResetFailedObjective(
        Guid eventId,
        Guid eventGameId,
        Guid objectiveId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<FailedObjectivesEndpoint> logger,
        CancellationToken ct,
        Guid? onBehalfOfUserId = null,
        Guid? expectedTrialRunId = null)
    {
        // Same resolution as FailObjective, so a reset can only ever remove
        // the row a fail would have created.
        var (scope, error) = await ObjectiveOutcomeWrite.BeginAsync(
            eventId, eventGameId, objectiveId, principal, onBehalfOfUserId, expectedTrialRunId, db, ct);
        if (error is not null) return error;
        await using var write = scope!;
        var callerId = write.CallerId;
        var targetUserId = write.TargetUserId;
        var trialRunId = write.TrialRunId;

        var failed = await db.FailedObjectives
            .FirstOrDefaultAsync(f => f.ObjectiveId == objectiveId && f.UserId == targetUserId && f.TrialRunId == trialRunId, ct);
        if (failed is null)
            return Results.Problem(detail: "Failure not found.", statusCode: StatusCodes.Status404NotFound);

        db.FailedObjectives.Remove(failed);
        audit.Log(db, AuditEventTypes.ObjectiveUnfailed, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objectiveId,
            subjectUserId: targetUserId,
            before: new { failed.FailedAt, failed.InGameTimeMs, failed.TrialRunId });
        await db.SaveChangesAsync(ct);
        await write.CommitAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectiveUnfailed(objectiveId, targetUserId, eventId, callerId);
        return Results.NoContent();
    }
}
