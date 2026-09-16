using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events;

/// <summary>Which record a new completion/failure belongs to, or neither.</summary>
public enum ObjectiveWriteScope
{
    /// <summary>No trial slot exists for this competitor+game: the row is official.</summary>
    Official,

    /// <summary>A trial slot exists and is recording: the row belongs to that run.</summary>
    Trial,

    /// <summary>
    /// A trial slot exists but is not recording: nothing may be written. The
    /// competitor asked for a practice space on this game, so an official row
    /// here is the "I was in trial mode, why did my real score move?" bug, and
    /// a dormant run accepts nothing either. Start the run or disable trial
    /// mode to resolve.
    /// </summary>
    Blocked,
}

/// <summary>
/// Resolved write target for one competitor+game. <c>TrialRunId</c> is set only
/// for <see cref="ObjectiveWriteScope.Trial"/>.
/// </summary>
public readonly record struct ObjectiveWriteTarget(ObjectiveWriteScope Scope, Guid? TrialRunId)
{
    public bool IsBlocked => Scope == ObjectiveWriteScope.Blocked;
}

/// <summary>
/// The single decision point for official-vs-trial attribution.
///
/// Official progress and a trial run are mutually exclusive per
/// (event game, competitor): no trial once official rows exist, no official row
/// while a trial slot exists. Every write surface — manual complete/uncomplete,
/// manual fail/reset, and the connector — resolves through here so they cannot
/// disagree about where a row lands.
///
/// Both halves must be evaluated inside the caller's transaction, under
/// <see cref="ObjectiveOutcomeLock"/> for the same event game, or the two
/// rules race each other into the state they exist to forbid.
/// </summary>
public static class TrialRunLookup
{
    /// <summary>Shown when a write is refused because a dormant trial slot owns this game.</summary>
    public const string BlockedDetail =
        "Trial mode is enabled for this game, so nothing can be recorded against the official "
        + "score. Start the trial run to record practice progress, or disable trial mode to "
        + "record officially again.";

    /// <summary>Shown when a trial is refused because official progress already exists.</summary>
    public const string OfficialProgressDetail =
        "This game already has official progress for this competitor, so a trial run cannot be "
        + "enabled or started — a practice run must not compete with a real attempt. Clear those "
        + "completions first, or trial a different game.";

    /// <summary>
    /// Where a new completion/failure for this competitor+game must go. The
    /// trial slot's existence decides Official vs. not; its state decides Trial
    /// vs. Blocked.
    /// </summary>
    public static async Task<ObjectiveWriteTarget> ResolveWriteTargetAsync(
        AppDbContext db, Guid eventGameId, Guid userId, CancellationToken ct)
    {
        var trialRun = await db.TrialRuns
            .Where(t => t.EventGameId == eventGameId && t.UserId == userId)
            .Select(t => new { t.Id, t.State })
            .FirstOrDefaultAsync(ct);

        if (trialRun is null)
            return new ObjectiveWriteTarget(ObjectiveWriteScope.Official, null);

        return trialRun.State == TrialRunState.Running
            ? new ObjectiveWriteTarget(ObjectiveWriteScope.Trial, trialRun.Id)
            : new ObjectiveWriteTarget(ObjectiveWriteScope.Blocked, null);
    }

    /// <summary>
    /// Whether this competitor has any official (non-trial) completion or
    /// failure on this game — the condition that forbids a trial run.
    /// Deliberately counts failures: a failed objective is official progress,
    /// and a trial sitting beside it would make the real attempt unreadable.
    /// </summary>
    public static async Task<bool> HasOfficialOutcomeAsync(
        AppDbContext db, Guid eventGameId, Guid userId, CancellationToken ct)
    {
        var hasCompletion = await db.CompletedObjectives
            .AnyAsync(co => co.TrialRunId == null
                && co.UserId == userId
                && co.Objective.EventGameId == eventGameId, ct);
        if (hasCompletion) return true;

        return await db.FailedObjectives
            .AnyAsync(f => f.TrialRunId == null
                && f.UserId == userId
                && f.Objective.EventGameId == eventGameId, ct);
    }
}
