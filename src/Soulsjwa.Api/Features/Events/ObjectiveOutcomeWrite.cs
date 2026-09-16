using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events;

/// <summary>
/// Everything a manual objective-outcome write (complete, uncomplete, fail,
/// reset, fail the rest of a game) resolves before it touches a row, opened
/// as one unit: the running event, who the write is for, the game and
/// objective, competitor membership, the transaction and advisory lock, the
/// official-or-trial target, the enabled-game gate and the client's trial
/// assertion. The four handlers used to carry their own copies of this
/// sequence and had already drifted in check order. Dispose it to end the
/// transaction; call <see cref="CommitAsync"/> first when the write succeeded.
/// </summary>
internal sealed class ObjectiveOutcomeWriteScope(
    Guid callerId,
    Guid targetUserId,
    EventGame eventGame,
    Guid? trialRunId,
    IDbContextTransaction transaction) : IAsyncDisposable
{
    public Guid CallerId { get; } = callerId;

    /// <summary>The competitor the outcome is recorded for: the caller, or the on-behalf-of target.</summary>
    public Guid TargetUserId { get; } = targetUserId;

    public EventGame EventGame { get; } = eventGame;

    /// <summary>The recording trial run the write belongs to, or null for an official record.</summary>
    public Guid? TrialRunId { get; } = trialRunId;

    public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}

internal static class ObjectiveOutcomeWrite
{
    /// <param name="objectiveId">
    /// The objective the write names, or null for a write that spans every
    /// objective of the game (the game itself is still validated).
    /// </param>
    public static async Task<(ObjectiveOutcomeWriteScope? Scope, IResult? Error)> BeginAsync(
        Guid eventId,
        Guid eventGameId,
        Guid? objectiveId,
        ClaimsPrincipal principal,
        Guid? onBehalfOfUserId,
        Guid? expectedTrialRunId,
        AppDbContext db,
        CancellationToken ct)
    {
        var callerId = EventOwnership.GetUserId(principal);

        var (ev, gateError) = await EventContext.RequireRunningEventAsync(eventId, db, ct);
        if (gateError is not null) return (null, gateError);

        // Admins, the event's streamer, and delegated moderators can write on
        // behalf of a competitor via ?onBehalfOfUserId=; otherwise self only.
        var targetUserId = callerId;
        if (onBehalfOfUserId.HasValue && onBehalfOfUserId.Value != callerId)
        {
            if (await EventOwnership.RequireCanCompleteForStreamerAsync(ev!, principal, onBehalfOfUserId.Value, db, ct) is { } err)
                return (null, err);

            targetUserId = onBehalfOfUserId.Value;
        }

        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);
        if (eventGame is null)
            return (null, Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound));

        // The objective must belong to the game named in the route. Otherwise
        // the caller picks which game the trial is resolved against: naming a
        // trial-free game while a run records on the objective's real game
        // resolved to "official" and wrote (or deleted) the official record,
        // locking and evicting the wrong game.
        if (objectiveId is { } namedObjectiveId)
        {
            var objectiveInGame = await db.Objectives
                .AnyAsync(o => o.Id == namedObjectiveId && o.EventGameId == eventGame.Id, ct);
            if (!objectiveInGame)
                return (null, Results.Problem(detail: "Objective not found.", statusCode: StatusCodes.Status404NotFound));
        }

        // Being a streamer (already proved above for on-behalf-of) is not enough:
        // the target must also be enrolled as a competitor to receive scoring.
        var isCompetitor = await db.EventCompetitors
            .AnyAsync(ec => ec.EventId == eventId && ec.UserId == targetUserId, ct);
        if (!isCompetitor)
            return (null, Results.Problem(
                detail: "User is not a competitor in this event.",
                statusCode: StatusCodes.Status403Forbidden));

        // Transaction + advisory lock: which row a write creates or removes
        // depends on the trial resolved a moment earlier, so a run starting or
        // stopping in between would target the other record. Owned by the
        // scope from here on; an error path below disposes it.
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await ObjectiveOutcomeLock.AcquireAsync(db, eventGame.Id, ct);

            // Attribute to the competitor's recording trial run for this game,
            // else official. A dormant slot refuses the write rather than
            // quietly making it official.
            var target = await TrialRunLookup.ResolveWriteTargetAsync(db, eventGame.Id, targetUserId, ct);
            if (target.IsBlocked)
                return await FailAsync(transaction, Results.Problem(
                    detail: TrialRunLookup.BlockedDetail, statusCode: StatusCodes.Status409Conflict));
            var trialRunId = target.TrialRunId;

            // Trial runs work on any game, so IsEnabled only gates official
            // records. Checked here — inside the transaction, under the lock,
            // against the same trialRunId the write uses — because resolving
            // it earlier leaves a window for the trial to stop in between and
            // the write to land officially on a disabled game.
            if (!eventGame.IsEnabled && trialRunId is null)
                return await FailAsync(transaction, Results.Problem(
                    detail: "Game is not enabled.", statusCode: StatusCodes.Status403Forbidden));

            // The client may assert which run it believed it was writing to.
            // The Trial tab decides whether ticking is allowed from a polled
            // state, so without this a run stopping inside that window would
            // silently land an official completion on the live scoreboard.
            if (expectedTrialRunId.HasValue && trialRunId != expectedTrialRunId.Value)
                return await FailAsync(transaction, Results.Problem(
                    detail: "That trial run is no longer recording. Refresh before continuing.",
                    statusCode: StatusCodes.Status409Conflict));

            return (new ObjectiveOutcomeWriteScope(callerId, targetUserId, eventGame, trialRunId, transaction), null);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static async Task<(ObjectiveOutcomeWriteScope?, IResult?)> FailAsync(IDbContextTransaction transaction, IResult error)
    {
        await transaction.DisposeAsync();
        return (null, error);
    }
}
