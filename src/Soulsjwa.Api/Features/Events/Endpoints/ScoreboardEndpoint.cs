using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

using Soulsjwa.Api.Features.Audits;

using Soulsjwa.Api.Features.Audits.Services;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record ScoreboardResponse(
    List<ScoreboardEntry> Entries,
    string TieBreakMode);

public sealed record ScoreboardEntry(
    Guid UserId,
    string DisplayName,
    string TwitchLogin,
    string? ProfileImageUrl,
    bool IsLive,
    int TotalScore,
    int CompletedCount,
    bool IsFinished,
    DateTime? LastCompletedAt,
    long? TotalInGameTimeMs,
    int Rank,
    List<GameBreakdown> Games,
    int FailedCount = 0,
    string Status = nameof(ObjectiveOutcome.Pending)) : IScoreboardSortable;

public sealed record GameBreakdown(
    Guid EventGameId,
    string GameName,
    int Score,
    int CompletedCount,
    int TotalObjectives,
    List<ObjectiveDetail> Objectives,
    List<CompetitorInfoResponse> Infos,
    bool HasDeathClip,
    int FailedCount = 0,
    bool IsEnabled = false,
    bool IsTrialActive = false,
    TrialProgress? Trial = null,
    bool HasTrialRun = false);

public sealed record ObjectiveDetail(
    Guid ObjectiveId,
    string Name,
    int Score,
    string? Category,
    bool IsCompleted,
    DateTime? CompletedAt,
    bool IsFailed = false,
    DateTime? FailedAt = null,
    string Status = nameof(ObjectiveOutcome.Pending),
    TrialObjectiveState? Trial = null);

/// <summary>
/// A competitor's progress inside their own trial run for one game (SPEC
/// §7.13), reported alongside — never merged into — the official figures on the
/// same <see cref="GameBreakdown"/>. Non-null once the run has begun (any state
/// but <see cref="TrialRunState.NotStarted"/>).
///
/// Deliberately absent from <see cref="ScoreboardEntry"/> and so from
/// <see cref="IScoreboardSortable"/>: ranking is generic over that interface,
/// so trial progress cannot move a competitor's position. Consumers needing a
/// per-competitor total sum it from the games they display, which is also the
/// only answer that stays correct when the visible game set is filtered.
/// </summary>
public sealed record TrialProgress(
    Guid TrialRunId,
    string State,
    int Score,
    int CompletedCount,
    int FailedCount,
    DateTime? LastCompletedAt);

/// <summary>
/// One objective's state within the enclosing game's <see cref="TrialProgress"/>.
/// Independent of the official outcome on the same <see cref="ObjectiveDetail"/>:
/// a trial is a fresh attempt whose rows are discarded on reset or disable.
/// </summary>
public sealed record TrialObjectiveState(
    bool IsCompleted,
    DateTime? CompletedAt,
    bool IsFailed,
    DateTime? FailedAt,
    string Status);

public sealed record SetLiveRequest(bool IsLive);

/// <summary>
/// Per-competitor aggregate for dashboard-style endpoints that need totals, not
/// the full objective-by-objective matrix <see cref="ScoreboardEntry"/> carries.
/// </summary>
public sealed record CompetitorSummary(
    Guid EventId,
    Guid UserId,
    string DisplayName,
    int TotalScore,
    int CompletedCount,
    int FailedCount,
    int TotalObjectives,
    long? TotalInGameTimeMs,
    DateTime? LastCompletedAt,
    int Rank = 0) : IScoreboardSortable;

/// <summary>
/// The scoreboard read and the competitor's live flag. Handlers are
/// <c>internal</c> rather than <c>private</c> so <c>Soulsjwa.IntegrationTests</c>
/// can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class ScoreboardEndpoint : IEndpoint
{
    private sealed record CompletedObjectiveRow(Guid UserId, Guid ObjectiveId, DateTime CompletedAt, long? InGameTimeMs);
    private sealed record FailedObjectiveRow(Guid UserId, Guid ObjectiveId, DateTime FailedAt, long? InGameTimeMs);
    private sealed record TrialRunRow(Guid Id, Guid UserId, Guid EventGameId, TrialRunState State);
    private sealed record TrialOutcomeRow(Guid TrialRunId, Guid ObjectiveId, DateTime At);

    /// <summary>
    /// Every trial run touching a set of event games, plus the completions and
    /// failures recorded under the ones that have started. Kept entirely
    /// separate from the official lookups so the two can never be conflated.
    /// </summary>
    private sealed record TrialData(
        IReadOnlySet<(Guid UserId, Guid EventGameId)> Enabled,
        IReadOnlySet<(Guid UserId, Guid EventGameId)> Active,
        IReadOnlyDictionary<(Guid UserId, Guid EventGameId), TrialRunRow> StartedRuns,
        IReadOnlyDictionary<(Guid TrialRunId, Guid ObjectiveId), TrialOutcomeRow> Completions,
        IReadOnlyDictionary<(Guid TrialRunId, Guid ObjectiveId), TrialOutcomeRow> Failures)
    {
        public static TrialData Empty { get; } = new(
            new HashSet<(Guid, Guid)>(),
            new HashSet<(Guid, Guid)>(),
            new Dictionary<(Guid, Guid), TrialRunRow>(),
            new Dictionary<(Guid, Guid), TrialOutcomeRow>(),
            new Dictionary<(Guid, Guid), TrialOutcomeRow>());
    }

    /// <summary>
    /// Loads trial state for <paramref name="eventGameIds"/>. Rows are fetched
    /// by trial run id (an indexed FK), leaving the official queries' partial
    /// <c>TrialRunId IS NULL</c> indexes untouched. A
    /// <see cref="TrialRunState.NotStarted"/> run reports no figures but still
    /// appears in <see cref="TrialData.Enabled"/>: the slot's existence is what
    /// blocks official writes on that game, and the UI must be able to say so.
    /// </summary>
    private static async Task<TrialData> LoadTrialDataAsync(
        IReadOnlyList<Guid> eventGameIds, AppDbContext db, CancellationToken ct)
    {
        if (eventGameIds.Count == 0) return TrialData.Empty;

        var runs = await db.TrialRuns
            .Where(t => eventGameIds.Contains(t.EventGameId))
            .Select(t => new TrialRunRow(t.Id, t.UserId, t.EventGameId, t.State))
            .ToListAsync(ct);

        var enabled = runs.Select(t => (t.UserId, t.EventGameId)).ToHashSet();
        var active = runs
            .Where(t => t.State == TrialRunState.Running)
            .Select(t => (t.UserId, t.EventGameId))
            .ToHashSet();

        var started = runs.Where(t => t.State != TrialRunState.NotStarted).ToList();
        var startedIds = started.Select(t => t.Id).ToList();
        var startedRuns = started.ToDictionary(t => (t.UserId, t.EventGameId));
        if (startedIds.Count == 0)
            return TrialData.Empty with { Enabled = enabled, Active = active };

        var completions = await db.CompletedObjectives
            .Where(co => co.TrialRunId != null && startedIds.Contains(co.TrialRunId.Value))
            .Select(co => new TrialOutcomeRow(co.TrialRunId!.Value, co.ObjectiveId, co.CompletedAt))
            .ToListAsync(ct);

        var failures = await db.FailedObjectives
            .Where(f => f.TrialRunId != null && startedIds.Contains(f.TrialRunId.Value))
            .Select(f => new TrialOutcomeRow(f.TrialRunId!.Value, f.ObjectiveId, f.FailedAt))
            .ToListAsync(ct);

        // (TrialRunId, ObjectiveId) is unique: a run belongs to one competitor
        // and the partial unique indexes allow one row per objective within it.
        return new TrialData(
            enabled,
            active,
            startedRuns,
            completions.ToDictionary(r => (r.TrialRunId, r.ObjectiveId)),
            failures.ToDictionary(r => (r.TrialRunId, r.ObjectiveId)));
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.EventById);

        group.MapGet("/scoreboard", GetScoreboard)
            .WithName("GetEventScoreboard")
            .WithSummary("Gets the public scoreboard for an event")
            .Produces<ScoreboardResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .CacheOutput("Scoreboard")
            .AllowAnonymous();

        group.MapPost("/live", SetLive)
            .WithName("SetCompetitorLiveStatus")
            .WithSummary("Sets live status for the current competitor or an authorized streamer's moderator/admin target")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    internal static async Task<IResult> GetScoreboard(
        Guid eventId,
        AppDbContext db,
        CancellationToken ct)
    {
        var response = await BuildAsync(eventId, db, ct);
        return response is null
            ? Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(response);
    }

    /// <summary>
    /// Computes the full per-event scoreboard, or <c>null</c> if the event does
    /// not exist. Reused by the token-gated overlay endpoint so the in-app
    /// scoreboard and the OBS overlay always render identical data.
    /// </summary>
    internal static async Task<ScoreboardResponse?> BuildAsync(
        Guid eventId,
        AppDbContext db,
        CancellationToken ct,
        bool includeArchived = false,
        bool enabledGamesOnly = false)
    {
        // The single-event case of the batch build. It used to be a second,
        // hand-maintained copy of the same six queries and the same assembly,
        // which is one copy too many for something the scoreboard, the
        // overlay and My Events all have to agree on.
        var built = await BuildBatchAsync([eventId], db, ct, includeArchived, enabledGamesOnly);
        return built.GetValueOrDefault(eventId);
    }

    /// <summary>
    /// Builds scoreboards for multiple events in a constant number of queries,
    /// for dashboard endpoints where per-event round-trips would be N+1.
    /// </summary>
    internal static async Task<Dictionary<Guid, ScoreboardResponse>> BuildBatchAsync(
        IReadOnlyList<Guid> eventIds,
        AppDbContext db,
        CancellationToken ct,
        bool includeArchived = false,
        bool enabledGamesOnly = false)
    {
        if (eventIds.Count == 0) return [];

        var events = includeArchived ? db.Events.IgnoreQueryFilters() : db.Events;
        var eventMeta = await events
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => new { e.Id, e.TieBreakMode })
            .ToListAsync(ct);

        var gameQuery = db.EventGames.Where(eg => eventIds.Contains(eg.EventId));
        if (enabledGamesOnly)
            gameQuery = gameQuery.Where(eg => eg.IsEnabled);
        var allEventGames = await gameQuery
            .Include(eg => eg.KnownGame)
            .Include(eg => eg.Objectives)
            .Include(eg => eg.CompetitorInfos)
            .ToListAsync(ct);

        var allCompetitors = await db.EventCompetitors
            .Where(ec => eventIds.Contains(ec.EventId))
            .Include(ec => ec.User)
            .ToListAsync(ct);

        var allObjectiveIds = allEventGames
            .SelectMany(eg => eg.Objectives)
            .Select(o => o.Id)
            .ToList();

        // Official scoring always filters TrialRunId IS NULL — see BuildAsync.
        var allCompletedObjectives = allObjectiveIds.Count == 0
            ? []
            : await db.CompletedObjectives
                .Where(co => allObjectiveIds.Contains(co.ObjectiveId) && co.TrialRunId == null)
                .Select(co => new CompletedObjectiveRow(
                    co.UserId,
                    co.ObjectiveId,
                    co.CompletedAt,
                    co.InGameTimeMs))
                .ToListAsync(ct);

        var completionLookup = allCompletedObjectives
            .GroupBy(co => (co.UserId, co.ObjectiveId))
            .ToDictionary(g => g.Key, g => g.MaxBy(x => x.CompletedAt)!);

        var allFailedObjectives = allObjectiveIds.Count == 0
            ? []
            : await db.FailedObjectives
                .Where(f => allObjectiveIds.Contains(f.ObjectiveId) && f.TrialRunId == null)
                .Select(f => new FailedObjectiveRow(
                    f.UserId,
                    f.ObjectiveId,
                    f.FailedAt,
                    f.InGameTimeMs))
                .ToListAsync(ct);

        var failureLookup = allFailedObjectives
            .GroupBy(f => (f.UserId, f.ObjectiveId))
            .ToDictionary(g => g.Key, g => g.MaxBy(x => x.FailedAt)!);

        var allEventGameIds = allEventGames.Select(eg => eg.Id).ToList();
        var allTrialData = await LoadTrialDataAsync(allEventGameIds, db, ct);

        var gamesByEvent = allEventGames
            .GroupBy(eg => eg.EventId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var competitorsByEvent = allCompetitors
            .GroupBy(ec => ec.EventId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var objectiveToEventId = allEventGames
            .SelectMany(eg => eg.Objectives.Select(o => new { ObjectiveId = o.Id, eg.EventId }))
            .ToDictionary(x => x.ObjectiveId, x => x.EventId);

        var completedByEvent = allCompletedObjectives
            .GroupBy(co => objectiveToEventId[co.ObjectiveId])
            .ToDictionary(g => g.Key, g => g.ToList());

        var failedByEvent = allFailedObjectives
            .GroupBy(f => objectiveToEventId[f.ObjectiveId])
            .ToDictionary(g => g.Key, g => g.ToList());

        var totalObjectivesByEvent = allObjectiveIds
            .GroupBy(id => objectiveToEventId[id])
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new Dictionary<Guid, ScoreboardResponse>(eventMeta.Count);
        foreach (var ev in eventMeta)
        {
            var evGames = gamesByEvent.GetValueOrDefault(ev.Id) ?? [];
            var evCompetitors = competitorsByEvent.GetValueOrDefault(ev.Id) ?? [];
            var evCompleted = completedByEvent.GetValueOrDefault(ev.Id) ?? [];
            var evFailed = failedByEvent.GetValueOrDefault(ev.Id) ?? [];
            var totalObjectiveCount = totalObjectivesByEvent.GetValueOrDefault(ev.Id);

            var entries = BuildEntries(
                evCompetitors, evGames, completionLookup, evCompleted,
                failureLookup, evFailed, totalObjectiveCount, allTrialData);

            var sorted = ScoreboardRanking.Sort(entries);
            var ranks = ScoreboardRanking.AssignRanks(sorted, ev.TieBreakMode);
            var ranked = sorted
                .Select((entry, i) => entry with { Rank = ranks[i] })
                .ToList();

            result[ev.Id] = new ScoreboardResponse(ranked, ev.TieBreakMode.ToString());
        }
        return result;
    }

    /// <summary>
    /// Aggregates only, via grouped queries in the database, instead of
    /// materialising the full competitor × game × objective matrix
    /// <see cref="BuildBatchAsync"/> produces. Competitors come back unranked
    /// (<c>Rank = 0</c>): the caller sorts and ranks per event via
    /// <see cref="ScoreboardRanking"/> once it has that event's
    /// <c>TieBreakMode</c>.
    /// </summary>
    internal static async Task<Dictionary<Guid, IReadOnlyList<CompetitorSummary>>> BuildSummaryBatchAsync(
        IReadOnlyList<Guid> eventIds,
        AppDbContext db,
        CancellationToken ct,
        bool enabledGamesOnly = false)
    {
        if (eventIds.Count == 0) return [];

        var roster = await db.EventCompetitors
            .Where(ec => eventIds.Contains(ec.EventId))
            .Select(ec => new { ec.EventId, ec.UserId, ec.User.DisplayName })
            .ToListAsync(ct);

        var objectivesQuery = db.Objectives
            .Where(o => o.EventGameId != null && eventIds.Contains(o.EventGame!.EventId));
        if (enabledGamesOnly)
            objectivesQuery = objectivesQuery.Where(o => o.EventGame!.IsEnabled);
        var totalObjectivesByEvent = await objectivesQuery
            .GroupBy(o => o.EventGame!.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, ct);

        // Official scoring always filters TrialRunId IS NULL — see BuildAsync.
        var completedQuery = db.CompletedObjectives
            .Where(co => co.TrialRunId == null
                && co.Objective.EventGameId != null
                && eventIds.Contains(co.Objective.EventGame!.EventId));
        if (enabledGamesOnly)
            completedQuery = completedQuery.Where(co => co.Objective.EventGame!.IsEnabled);
        var completedAgg = await completedQuery
            .GroupBy(co => new { EventId = co.Objective.EventGame!.EventId, co.UserId })
            .Select(g => new
            {
                g.Key.EventId,
                g.Key.UserId,
                Score = g.Sum(x => x.Objective.Score),
                Count = g.Count(),
                MaxCompletedAt = g.Max(x => (DateTime?)x.CompletedAt),
                // Preserve BuildEntries' all-or-nothing in-game-time semantics
                // (a partially-timed competitor reports null, not an
                // understated sum) without relying on a bool_and/CASE
                // translation: count nulls in the database, decide in memory.
                NullTimeCount = g.Count(x => x.InGameTimeMs == null),
                SumTimeOrZero = g.Sum(x => x.InGameTimeMs ?? 0),
            })
            .ToDictionaryAsync(x => (x.EventId, x.UserId), ct);

        var failedQuery = db.FailedObjectives
            .Where(f => f.TrialRunId == null
                && f.Objective.EventGameId != null
                && eventIds.Contains(f.Objective.EventGame!.EventId));
        if (enabledGamesOnly)
            failedQuery = failedQuery.Where(f => f.Objective.EventGame!.IsEnabled);
        var failedAgg = await failedQuery
            .GroupBy(f => new { EventId = f.Objective.EventGame!.EventId, f.UserId })
            .Select(g => new { g.Key.EventId, g.Key.UserId, Count = g.Count() })
            .ToDictionaryAsync(x => (x.EventId, x.UserId), x => x.Count, ct);

        var result = new Dictionary<Guid, List<CompetitorSummary>>();
        foreach (var member in roster)
        {
            var key = (member.EventId, member.UserId);
            var completed = completedAgg.GetValueOrDefault(key);
            var failedCount = failedAgg.GetValueOrDefault(key);
            var totalObjectives = totalObjectivesByEvent.GetValueOrDefault(member.EventId);

            long? totalInGameTimeMs = completed is { Count: > 0, NullTimeCount: 0 }
                ? completed.SumTimeOrZero
                : null;

            var summary = new CompetitorSummary(
                member.EventId,
                member.UserId,
                member.DisplayName,
                completed?.Score ?? 0,
                completed?.Count ?? 0,
                failedCount,
                totalObjectives,
                totalInGameTimeMs,
                completed?.MaxCompletedAt);

            if (!result.TryGetValue(member.EventId, out var list))
                result[member.EventId] = list = [];
            list.Add(summary);
        }

        return result.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<CompetitorSummary>)kv.Value);
    }

    /// <summary>
    /// Per-(event, competitor) most recent <see cref="EventGameCompetitorInfo"/>
    /// timestamp and type, for the "last activity" figure on dashboards.
    /// Projects straight to scalars rather than loading each event-game's full
    /// <c>CompetitorInfos</c> collection just to find the newest row.
    /// </summary>
    internal static async Task<Dictionary<(Guid EventId, Guid UserId), (DateTime CreatedAt, string Type)>>
        BuildLastInfoActivityBatchAsync(IReadOnlyList<Guid> eventIds, AppDbContext db, CancellationToken ct)
    {
        if (eventIds.Count == 0) return [];

        var rows = await db.EventGameCompetitorInfos
            .Where(i => eventIds.Contains(i.EventGame.EventId))
            .Select(i => new { EventId = i.EventGame.EventId, i.UserId, i.CreatedAt, Type = i.Type.ToString() })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => (r.EventId, r.UserId))
            .ToDictionary(g => g.Key, g =>
            {
                var latest = g.MaxBy(r => r.CreatedAt)!;
                return (latest.CreatedAt, latest.Type);
            });
    }

    /// <summary>
    /// This objective's state inside <paramref name="trialRun"/>, or
    /// <c>null</c> when the competitor has no started trial for the game.
    /// Mirrors the official path's precedence: a completion outranks a
    /// failure, so the two are never both reported.
    /// </summary>
    private static TrialObjectiveState? BuildTrialObjectiveState(
        TrialRunRow? trialRun, Guid objectiveId, TrialData trial)
    {
        if (trialRun is null) return null;

        var completion = trial.Completions.GetValueOrDefault((trialRun.Id, objectiveId));
        var failure = completion is null
            ? trial.Failures.GetValueOrDefault((trialRun.Id, objectiveId))
            : null;
        var isCompleted = completion is not null;
        var isFailed = failure is not null;
        return new TrialObjectiveState(
            isCompleted,
            completion?.At,
            isFailed,
            failure?.At,
            ObjectiveOutcomeCalculator.ForObjective(isCompleted, isFailed).ToString());
    }

    private static List<ScoreboardEntry> BuildEntries(
        IEnumerable<EventCompetitor> competitors,
        IEnumerable<EventGame> eventGames,
        IReadOnlyDictionary<(Guid UserId, Guid ObjectiveId), CompletedObjectiveRow> completionLookup,
        List<CompletedObjectiveRow> completedObjectives,
        IReadOnlyDictionary<(Guid UserId, Guid ObjectiveId), FailedObjectiveRow> failureLookup,
        List<FailedObjectiveRow> failedObjectives,
        int totalObjectiveCount,
        TrialData? trialData = null)
    {
        var trial = trialData ?? TrialData.Empty;
        var eventGamesList = eventGames as IReadOnlyList<EventGame> ?? eventGames.ToList();
        return competitors.Select(competitor =>
        {
            var games = eventGamesList
                .Select(eventGame =>
                {
                    var trialRun = trial.StartedRuns.GetValueOrDefault((competitor.UserId, eventGame.Id));
                    var objectives = eventGame.Objectives
                        .Select(objective =>
                        {
                            var completion = completionLookup.GetValueOrDefault((competitor.UserId, objective.Id));
                            var failure = completion is null
                                ? failureLookup.GetValueOrDefault((competitor.UserId, objective.Id))
                                : null;
                            var isCompleted = completion is not null;
                            var isFailed = failure is not null;
                            return new ObjectiveDetail(
                                objective.Id,
                                objective.Name,
                                objective.Score,
                                objective.Category,
                                isCompleted,
                                completion?.CompletedAt,
                                isFailed,
                                failure?.FailedAt,
                                ObjectiveOutcomeCalculator.ForObjective(isCompleted, isFailed).ToString(),
                                BuildTrialObjectiveState(trialRun, objective.Id, trial));
                        })
                        .ToList();

                    var infos = eventGame.CompetitorInfos
                        .Where(i => i.UserId == competitor.UserId)
                        .OrderBy(i => i.CreatedAt)
                        .Select(i => new CompetitorInfoResponse(
                            i.Id, i.EventGameId, i.UserId, i.Type.ToString(),
                            i.Url, i.Text, i.CreatedById, i.CreatedAt, i.UpdatedAt))
                        .ToList();
                    var hasDeathClip = infos.Any(i =>
                        string.Equals(i.Type, nameof(EventGameCompetitorInfoType.DeathClip), StringComparison.Ordinal));

                    return new GameBreakdown(
                        eventGame.Id,
                        eventGame.CustomGameName ?? eventGame.KnownGame?.Name ?? "Unknown",
                        objectives.Where(o => o.IsCompleted).Sum(o => o.Score),
                        objectives.Count(o => o.IsCompleted),
                        objectives.Count,
                        objectives,
                        infos,
                        hasDeathClip,
                        objectives.Count(o => o.IsFailed),
                        eventGame.IsEnabled,
                        trial.Active.Contains((competitor.UserId, eventGame.Id)),
                        trialRun is null ? null : new TrialProgress(
                            trialRun.Id,
                            trialRun.State.ToString(),
                            objectives.Where(o => o.Trial?.IsCompleted == true).Sum(o => o.Score),
                            objectives.Count(o => o.Trial?.IsCompleted == true),
                            objectives.Count(o => o.Trial?.IsFailed == true),
                            objectives.Max(o => o.Trial?.CompletedAt)),
                        trial.Enabled.Contains((competitor.UserId, eventGame.Id)));
                })
                .ToList();

            var completedCount = games.Sum(g => g.CompletedCount);
            var failedCount = games.Sum(g => g.FailedCount);
            var completionsForUser = completedObjectives
                .Where(co => co.UserId == competitor.UserId)
                .ToList();
            var lastCompletedAt = completionsForUser.Count == 0
                ? (DateTime?)null
                : completionsForUser.Max(co => co.CompletedAt);

            // Only report a total in-game time when every completion has
            // one — otherwise the sum understates the player's true time
            // and would unfairly beat a competitor whose completions are
            // fully timed.
            long? totalInGameTimeMs = completionsForUser.Count > 0
                && completionsForUser.All(co => co.InGameTimeMs.HasValue)
                ? completionsForUser.Sum(co => co.InGameTimeMs!.Value)
                : null;

            return new ScoreboardEntry(
                competitor.UserId,
                competitor.User.DisplayName,
                competitor.User.TwitchLogin,
                competitor.User.ProfileImageUrl,
                competitor.IsLive,
                games.Sum(g => g.Score),
                completedCount,
                totalObjectiveCount > 0 && completedCount + failedCount == totalObjectiveCount,
                lastCompletedAt,
                totalInGameTimeMs,
                0,
                games,
                failedCount,
                ObjectiveOutcomeCalculator.ForCompetitor(totalObjectiveCount, completedCount, failedCount).ToString());
        })
        .ToList();
    }

    internal static async Task<IResult> SetLive(
        Guid eventId,
        SetLiveRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        CancellationToken ct,
        Guid? onBehalfOfUserId = null)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var callerId = EventOwnership.GetUserId(principal);
        var targetUserId = callerId;

        if (onBehalfOfUserId.HasValue && onBehalfOfUserId.Value != callerId)
        {
            if (await EventOwnership.RequireCanCompleteForStreamerAsync(ev!, principal, onBehalfOfUserId.Value, db, ct) is { } err)
                return err;

            targetUserId = onBehalfOfUserId.Value;
        }

        var competitor = await db.EventCompetitors
            .FirstOrDefaultAsync(ec => ec.EventId == eventId && ec.UserId == targetUserId, ct);

        if (competitor is null)
        {
            return targetUserId == callerId && !EventOwnership.IsAdmin(principal)
                ? Results.Problem(detail: "User is not a competitor in this event.", statusCode: StatusCodes.Status403Forbidden)
                : Results.Problem(detail: "Competitor not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (competitor.IsLive != request.IsLive)
        {
            audit.Log(db, AuditEventTypes.StreamerLiveChanged, callerId, eventId: eventId,
                subjectUserId: targetUserId,
                before: new { competitor.IsLive }, after: new { request.IsLive });
        }
        competitor.IsLive = request.IsLive;
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        return Results.NoContent();
    }
}
