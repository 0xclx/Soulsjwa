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
using Soulsjwa.Api.Features.Games.Services;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events.Endpoints;

/// <summary>
/// Totals-only competitor row for compact listings. Every figure is official
/// (<c>TrialRunId IS NULL</c>); <see cref="IsTrialing"/> is the one trial signal
/// carried, so a surface showing a frozen score can explain why. It follows the
/// display rule used everywhere else — any run that has started, paused
/// included — rather than the narrower "still recording" flag, so a badge does
/// not appear on the scoreboard and vanish on a preview of it. For the trial's
/// own figures read the scoreboard's per-game <c>trial</c>, the only shape that
/// stays correct under game filtering.
/// </summary>
public sealed record ScoreEntry(Guid UserId, string DisplayName, string TwitchLogin, string? ProfileImageUrl, bool IsLive, int TotalScore, int CompletedCount, bool IsFinished, DateTime? LastCompletedAt, long? TotalInGameTimeMs, int Rank, int FailedCount = 0, string Status = nameof(ObjectiveOutcome.Pending), bool IsTrialing = false) : IScoreboardSortable;

/// <summary>
/// Manual override of a completion's wall-clock time. Admin/event-owner only;
/// bounded below by the event's <see cref="Event.CreatedAt"/> and above by now.
/// <see cref="Reason"/> is mandatory so the audit trail explains the edit.
/// </summary>
public sealed record EditCompletionTimeRequest(DateTimeOffset CompletedAt, string Reason);

/// <summary>
/// Official and trial completion of objectives, plus the totals-only scores
/// read.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly: the rules they
/// enforce — who may write, against which scope, what a second write does —
/// deserve a test each and involve no HTTP. The API suite keeps the route,
/// status-code and payload contract.
/// </summary>
public class CompletedObjectivesEndpoint : IEndpoint
{
    private const int MaxReasonLength = 2000;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.EventById);

        group.MapPost("/games/{eventGameId:guid}/objectives/{objectiveId:guid}/complete", CompleteObjective)
            .WithName("CompleteObjective")
            .WithSummary("Checks off an objective for the current user (must be a competitor)")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapDelete("/games/{eventGameId:guid}/objectives/{objectiveId:guid}/complete", UncompleteObjective)
            .WithName("UncompleteObjective")
            .WithSummary("Unchecks an objective for the current user")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPatch("/games/{eventGameId:guid}/objectives/{objectiveId:guid}/completions/{userId:guid}",
                EditCompletionTime)
            .WithName("EditObjectiveCompletionTime")
            .WithSummary(
                "Adjusts the recorded completion time for an existing objective completion " +
                "(admin or event owner only). Re-ranking happens automatically on the next " +
                "scoreboard read because the scoreboard cache is evicted on success.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapGet("/scores", GetScores)
            .WithName("GetEventScores")
            .WithSummary("Gets scores for all competitors in an event")
            .Produces<List<ScoreEntry>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .CacheOutput("Scoreboard")
            .AllowAnonymous();
    }

    internal static async Task<IResult> CompleteObjective(
        Guid eventId,
        Guid eventGameId,
        Guid objectiveId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<CompletedObjectivesEndpoint> logger,
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
                detail: "Objective already completed.",
                statusCode: StatusCodes.Status409Conflict);

        var alreadyFailed = await db.FailedObjectives
            .AnyAsync(f => f.ObjectiveId == objectiveId && f.UserId == targetUserId && f.TrialRunId == trialRunId, ct);
        if (alreadyFailed)
            return Results.Problem(
                detail: "Objective has already failed. Reset it before completing.",
                statusCode: StatusCodes.Status409Conflict);

        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = objectiveId,
            UserId = targetUserId,
            TrialRunId = trialRunId,
        });

        audit.Log(db, AuditEventTypes.ObjectiveCompleted, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objectiveId,
            subjectUserId: targetUserId,
            after: new { CompletedAt = DateTime.UtcNow, TrialRunId = trialRunId });

        await db.SaveChangesAsync(ct);
        // A new OFFICIAL completion changes `competitorCompletions` for every
        // OTHER pending competitor on this objective — resolve count-only
        // fail rules for them immediately (see FailRuleCascadeEvaluator).
        // Trial completions are private practice and must never cascade.
        if (trialRunId is null)
            await FailRuleCascadeEvaluator.ApplyAsync(db, [objectiveId], targetUserId, ct);
        await write.CommitAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectiveCompleted(objectiveId, targetUserId, eventId, callerId);
        return Results.Created();
    }

    internal static async Task<IResult> UncompleteObjective(
        Guid eventId,
        Guid eventGameId,
        Guid objectiveId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<CompletedObjectivesEndpoint> logger,
        CancellationToken ct,
        Guid? onBehalfOfUserId = null,
        Guid? expectedTrialRunId = null)
    {
        // Same resolution as CompleteObjective, so an untick can only ever
        // remove the row a tick would have created.
        var (scope, error) = await ObjectiveOutcomeWrite.BeginAsync(
            eventId, eventGameId, objectiveId, principal, onBehalfOfUserId, expectedTrialRunId, db, ct);
        if (error is not null) return error;
        await using var write = scope!;
        var callerId = write.CallerId;
        var targetUserId = write.TargetUserId;
        var trialRunId = write.TrialRunId;

        var completed = await db.CompletedObjectives
            .FirstOrDefaultAsync(co => co.ObjectiveId == objectiveId && co.UserId == targetUserId && co.TrialRunId == trialRunId, ct);
        if (completed is null)
            return Results.Problem(detail: "Completion not found.", statusCode: StatusCodes.Status404NotFound);

        db.CompletedObjectives.Remove(completed);
        audit.Log(db, AuditEventTypes.ObjectiveUncompleted, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objectiveId,
            subjectUserId: targetUserId,
            before: new { completed.CompletedAt, completed.InGameTimeMs, completed.TrialRunId });
        await db.SaveChangesAsync(ct);
        await write.CommitAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectiveUncompleted(objectiveId, targetUserId, eventId, callerId);
        return Results.NoContent();
    }

    /// <summary>
    /// Admin or event-owner edit of an existing completion's wall-clock time.
    /// In-game time is deliberately not editable — only <c>CompletedAt</c> is.
    /// The scoreboard cache is evicted so the next read re-ranks competitors
    /// whose position the edit may have shifted.
    /// </summary>
    internal static async Task<IResult> EditCompletionTime(
        Guid eventId,
        Guid eventGameId,
        Guid objectiveId,
        Guid userId,
        EditCompletionTimeRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<CompletedObjectivesEndpoint> logger,
        CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        // Admin or event owner only — deliberately not competitors or their
        // delegated moderators.
        if (EventOwnership.RequireOwner(ev, principal, "edit objective completion times") is { } ownerError)
            return ownerError;

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Reason))
            errors["Reason"] = ["Reason is required."];
        else if (request.Reason.Length > MaxReasonLength)
            errors["Reason"] = [$"Reason must be {MaxReasonLength} characters or fewer."];

        // Bounded to [event start, now]. There is no precise "started at"
        // timestamp, so CreatedAt is the closest available lower bound.
        var nowUtc = DateTime.UtcNow;
        var newCompletedAt = UtcTime.ToStorage(request.CompletedAt);
        if (newCompletedAt < ev.CreatedAt)
            errors["CompletedAt"] = ["CompletedAt must be on or after the event was created."];
        else if (newCompletedAt > nowUtc.AddMinutes(1))
            errors["CompletedAt"] = ["CompletedAt cannot be in the future."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);
        if (eventGame is null)
            return Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound);

        // Official records only — trial completions never reach the
        // scoreboard, so editing their time has no scoring effect to fix.
        var completed = await db.CompletedObjectives
            .FirstOrDefaultAsync(co => co.ObjectiveId == objectiveId && co.UserId == userId && co.TrialRunId == null, ct);
        if (completed is null)
            return Results.Problem(detail: "Completion not found.", statusCode: StatusCodes.Status404NotFound);

        // Sanity-check the (objective, eventGame, event) triple matches.
        var objective = await db.Objectives
            .FirstOrDefaultAsync(o => o.Id == objectiveId && o.EventGameId == eventGameId, ct);
        if (objective is null)
            return Results.Problem(detail: "Objective is not part of this event game.", statusCode: StatusCodes.Status404NotFound);

        var before = new { completed.CompletedAt };
        completed.CompletedAt = newCompletedAt;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.ObjectiveCompletionTimeEdited, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objectiveId,
            subjectUserId: userId,
            before: before,
            after: new { CompletedAt = newCompletedAt },
            reason: request.Reason.Trim());

        await db.SaveChangesAsync(ct);
        // Evicting makes the next /scores read re-compute ranks for everyone, so
        // an already-finished competitor's place still updates.
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectiveCompletionTimeEdited(objectiveId, userId, eventId, callerId);

        return Results.Ok(new { ObjectiveId = objectiveId, UserId = userId, CompletedAt = newCompletedAt });
    }

    internal static async Task<IResult> GetScores(
        Guid eventId,
        AppDbContext db,
        CancellationToken ct)
    {
        var scoreboard = await ScoreboardEndpoint.BuildAsync(eventId, db, ct);
        if (scoreboard is null)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        return Results.Ok(scoreboard.Entries.Select(entry => new ScoreEntry(
            entry.UserId,
            entry.DisplayName,
            entry.TwitchLogin,
            entry.ProfileImageUrl,
            entry.IsLive,
            entry.TotalScore,
            entry.CompletedCount,
            entry.IsFinished,
            entry.LastCompletedAt,
            entry.TotalInGameTimeMs,
            entry.Rank,
            entry.FailedCount,
            entry.Status,
            entry.Games.Any(game => game.Trial is not null))));
    }
}
