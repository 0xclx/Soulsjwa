using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Npgsql;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Games.Services;

/// <summary>
/// A new completion changes the shared <c>competitorCompletions</c> count, so
/// "count-only" fail rules (see <see cref="FailRuleAnalyzer"/>) are re-evaluated
/// here for every other still-pending competitor — they read none of that
/// competitor's own game state, so no submission from them is needed. Fail rules
/// that do read game state are left for that competitor's next submission.
/// </summary>
public static class FailRuleCascadeEvaluator
{
    private const string FailedObjectiveUniqueIndexName = "IX_FailedObjectives_ObjectiveId_UserId_Official";

    /// <summary>
    /// Returns how many competitors were newly marked failed. Objectives with no
    /// fail rule, or one that is not count-only, are silently skipped.
    /// </summary>
    public static async Task<int> ApplyAsync(
        AppDbContext db,
        IReadOnlyList<Guid> objectiveIds,
        Guid completingUserId,
        CancellationToken ct)
    {
        if (objectiveIds.Count == 0) return 0;

        var objectives = await db.Objectives.AsNoTracking()
            .Where(o => objectiveIds.Contains(o.Id) && o.FailRule != null && o.EventGameId != null)
            .ToListAsync(ct);
        var countOnlyObjectives = objectives
            .Where(o => FailRuleAnalyzer.IsCountOnly(o.FailRule))
            .ToList();
        if (countOnlyObjectives.Count == 0) return 0;

        var eventGameIds = countOnlyObjectives.Select(o => o.EventGameId!.Value).Distinct().ToList();
        var eventGamesById = await db.EventGames.AsNoTracking()
            .Where(eg => eventGameIds.Contains(eg.Id))
            .ToDictionaryAsync(eg => eg.Id, ct);

        var relevantObjectives = countOnlyObjectives
            .Where(o => eventGamesById.ContainsKey(o.EventGameId!.Value))
            .ToList();
        if (relevantObjectives.Count == 0) return 0;

        var eventIds = relevantObjectives
            .Select(o => eventGamesById[o.EventGameId!.Value].EventId)
            .Distinct()
            .ToList();
        var competitorIdsByEvent = (await db.EventCompetitors
                .Where(ec => eventIds.Contains(ec.EventId))
                .Select(ec => new { ec.EventId, ec.UserId })
                .ToListAsync(ct))
            .GroupBy(x => x.EventId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToHashSet());

        var relevantObjectiveIds = relevantObjectives.Select(o => o.Id).ToList();

        // Official rows only — a trial completion/failure must never cascade into
        // (or be affected by a cascade into) another competitor's official
        // progress. Callers already skip this entirely when the triggering row
        // was a trial completion; this filter is the second line of defense.
        var completedByObjective = (await db.CompletedObjectives
                .Where(co => relevantObjectiveIds.Contains(co.ObjectiveId) && co.TrialRunId == null)
                .Select(co => new { co.ObjectiveId, co.UserId })
                .ToListAsync(ct))
            .GroupBy(x => x.ObjectiveId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToHashSet());
        var failedByObjective = (await db.FailedObjectives
                .Where(f => relevantObjectiveIds.Contains(f.ObjectiveId) && f.TrialRunId == null)
                .Select(f => new { f.ObjectiveId, f.UserId })
                .ToListAsync(ct))
            .GroupBy(x => x.ObjectiveId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToHashSet());

        // Count-only rules read no submitted game-state variable, only the
        // injected competitorCompletions, so one empty object serves the whole
        // batch. Per call, not static: Evaluate injects into it in place.
        var emptyData = new JObject();

        var toInsert = new List<FailedObjective>();
        foreach (var objective in relevantObjectives)
        {
            var eventGame = eventGamesById[objective.EventGameId!.Value];
            if (!competitorIdsByEvent.TryGetValue(eventGame.EventId, out var competitorIds) || competitorIds.Count == 0)
                continue;

            // Restricted to people actually enrolled, matching how
            // ConnectorEndpoint computes the same `competitorCompletions`.
            // Completion rows outlive enrolment (removing a competitor deletes
            // only their EventCompetitor row), so without this the two paths
            // disagree on the count and a count-only fail rule fires through one
            // but not the other, depending on which evaluates first.
            var completedUserIds = (completedByObjective.GetValueOrDefault(objective.Id) ?? [])
                .Where(competitorIds.Contains)
                .ToHashSet();
            var failedUserIds = failedByObjective.GetValueOrDefault(objective.Id) ?? [];

            var pendingUserIds = competitorIds
                .Where(id => id != completingUserId && !completedUserIds.Contains(id) && !failedUserIds.Contains(id));

            // Every candidate is already excluded from completedUserIds by the
            // filter above, so the count is the candidate's "others" as-is.
            var otherCompletions = completedUserIds.Count;
            var rule = RuleJson.ParsedRuleCache.Get(objective.FailRule!);
            foreach (var candidateUserId in pendingUserIds)
            {
                if (!RuleEvaluator.Evaluate(rule, emptyData, otherCompletions)) continue;

                toInsert.Add(new FailedObjective
                {
                    ObjectiveId = objective.Id,
                    UserId = candidateUserId,
                });
            }
        }

        if (toInsert.Count == 0) return 0;

        db.FailedObjectives.AddRange(toInsert);
        try
        {
            await db.SaveChangesAsync(ct);
            return toInsert.Count;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The per-event-game advisory lock should prevent this, but a batched
            // insert fails as a whole — so fall back to one row at a time and
            // treat a conflicting row as already recorded.
            foreach (var entry in db.ChangeTracker.Entries<FailedObjective>().ToList())
                entry.State = EntityState.Detached;

            var newlyFailed = 0;
            foreach (var failedObjective in toInsert)
            {
                db.FailedObjectives.Add(failedObjective);
                try
                {
                    await db.SaveChangesAsync(ct);
                    newlyFailed++;
                }
                catch (DbUpdateException retryException)
                    when (retryException.InnerException is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: FailedObjectiveUniqueIndexName
                    })
                {
                    db.Entry(failedObjective).State = EntityState.Detached;
                }
            }

            return newlyFailed;
        }
    }
}
