using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Admin;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record MyEventsResponse(
    List<MyCompetitorEventResponse> Competitor,
    List<MyDelegatedEventResponse> Delegated,
    List<MyOwnedEventResponse> Owned,
    bool QuickCompleteEnabled);

public sealed record MyCompetitorEventResponse(
    Guid EventId,
    string EventName,
    string? UrlAlias,
    string Status,
    int Score,
    int Rank,
    int TotalCompetitors,
    int IncompleteObjectives,
    int TotalObjectives,
    DateTime? LastActivity,
    string? LastActivityType,
    int FailedObjectives = 0,
    string CompletionStatus = nameof(ObjectiveOutcome.Pending));

public sealed record MyDelegatedEventResponse(
    Guid EventId,
    string EventName,
    string? UrlAlias,
    string Status,
    Guid CompetitorId,
    string CompetitorName,
    int Score,
    int Rank,
    int TotalCompetitors,
    int IncompleteObjectives,
    int TotalObjectives,
    DateTime? LastActivity,
    string? LastActivityType,
    int FailedObjectives = 0,
    string CompletionStatus = nameof(ObjectiveOutcome.Pending));

public sealed record MyOwnedEventResponse(
    Guid EventId,
    string EventName,
    string? UrlAlias,
    string Status,
    int CompetitorCount,
    DateTime? LastActivity,
    string? LastActivityType);

public sealed record MyEventObjectivesResponse(
    Guid CompetitorId,
    string CompetitorName,
    List<MyEventGameResponse> Games);

public sealed record MyEventGameResponse(
    Guid GameId,
    string GameName,
    List<MyEventObjectiveResponse> Objectives,
    /// <summary>
    /// The competitor has a trial running for this game, so completions
    /// recorded now are attributed to that run and will not appear in this
    /// (official-only) list. Carried purely so the UI can say so and send the
    /// user to the Trial tab — no trial figures are reported here.
    /// </summary>
    bool IsTrialActive = false,
    /// <summary>
    /// A trial slot exists for this game in any state. Official writes are
    /// refused for as long as it does, so this — not
    /// <see cref="IsTrialActive"/> — is what makes the controls read-only:
    /// a dormant run blocks the write without recording anything either.
    /// </summary>
    bool HasTrialRun = false);

public sealed record MyEventObjectiveResponse(
    Guid ObjectiveId,
    string Name,
    bool Completed,
    DateTime? CompletedAt,
    int Score,
    bool Failed = false,
    DateTime? FailedAt = null,
    /// <summary>Boss objectives carry their in-game location here (from
    /// tools/generate_soulmemory_boss_data.py); other objectives may use it for
    /// any grouping label they choose.</summary>
    string? Category = null);

/// <summary>
/// The signed-in user's own events and objectives.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="CompletedObjectivesEndpoint"/> for why.
/// </summary>
public class MyEventsEndpoint : IEndpoint
{
    private const string LiveStatus = "live";
    private const string UpcomingStatus = "upcoming";
    private const string StoppedStatus = "stopped";
    private const string ArchivedStatus = "archived";
    private const string ObjectiveCompletedActivity = "objective_completed";
    private const string ClipAddedActivity = "clip_added";
    private const string NoteAddedActivity = "note_added";

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/me/events").RequireAuthorization();

        group.MapGet("/", List)
            .WithName("GetMyEvents")
            .WithSummary("Gets events involving the current user, grouped by role")
            .Produces<MyEventsResponse>();

        group.MapGet("/{eventId:guid}/objectives", GetObjectives)
            .WithName("GetMyEventObjectives")
            .WithSummary("Gets objectives for the current or delegated competitor")
            .Produces<MyEventObjectivesResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    internal static async Task<IResult> List(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = EventOwnership.GetUserId(principal);
        var eventRows = await db.Events
            .IgnoreQueryFilters()
            .Where(ev =>
                ev.CreatedById == userId
                || ev.Competitors.Any(competitor => competitor.UserId == userId)
                || ev.Competitors.Any(competitor =>
                    competitor.Moderators.Any(moderator => moderator.ModeratorUserId == userId)))
            .Select(ev => new MyEventRow(
                ev.Id,
                ev.Name,
                ev.UrlAlias,
                ev.IsArchived,
                ev.IsStarted,
                ev.StoppedAt != null,
                ev.CreatedById,
                ev.TieBreakMode,
                ev.Competitors
                    .Where(competitor => competitor.UserId == userId)
                    .Select(competitor => competitor.UserId)
                    .FirstOrDefault(),
                ev.Competitors
                    .Where(competitor =>
                        competitor.Moderators.Any(moderator => moderator.ModeratorUserId == userId))
                    .Select(competitor => new DelegatedCompetitorRow(
                        competitor.UserId,
                        competitor.User.DisplayName))
                    .ToList()))
            .ToListAsync(ct);

        var eventIds = eventRows.Select(row => row.Id).ToList();

        // Aggregate-only queries: a fixed number of grouped SQL queries
        // regardless of event count, instead of BuildBatchAsync's full
        // competitor × game × objective matrix. Ranking still needs the whole
        // roster per event, so sort/rank happens below per event.
        var summariesByEvent = await ScoreboardEndpoint.BuildSummaryBatchAsync(eventIds, db, ct, enabledGamesOnly: true);
        var lastInfoActivity = await ScoreboardEndpoint.BuildLastInfoActivityBatchAsync(eventIds, db, ct);

        var competitorEvents = new List<MyCompetitorEventResponse>();
        var delegatedEvents = new List<MyDelegatedEventResponse>();
        var ownedEvents = new List<MyOwnedEventResponse>();
        foreach (var row in eventRows)
        {
            if (!summariesByEvent.TryGetValue(row.Id, out var roster)) continue;

            var sorted = ScoreboardRanking.Sort(roster);
            var ranks = ScoreboardRanking.AssignRanks(sorted, row.TieBreakMode);
            var ranked = sorted.Select((s, i) => s with { Rank = ranks[i] }).ToList();

            var status = GetStatus(row);
            if (row.CompetitorId != Guid.Empty
                && ranked.FirstOrDefault(s => s.UserId == userId) is { } competitorSummary)
            {
                var activity = GetLastActivity(row.Id, competitorSummary, lastInfoActivity);
                competitorEvents.Add(new MyCompetitorEventResponse(
                    row.Id,
                    row.Name,
                    row.UrlAlias,
                    status,
                    competitorSummary.TotalScore,
                    competitorSummary.Rank,
                    ranked.Count,
                    competitorSummary.TotalObjectives - competitorSummary.CompletedCount - competitorSummary.FailedCount,
                    competitorSummary.TotalObjectives,
                    activity.At,
                    activity.Type,
                    competitorSummary.FailedCount,
                    GetCompletionStatus(competitorSummary)));
            }

            foreach (var delegated in row.DelegatedCompetitors)
            {
                if (ranked.FirstOrDefault(s => s.UserId == delegated.Id) is not { } delegatedSummary)
                    continue;

                var activity = GetLastActivity(row.Id, delegatedSummary, lastInfoActivity);
                delegatedEvents.Add(new MyDelegatedEventResponse(
                    row.Id,
                    row.Name,
                    row.UrlAlias,
                    status,
                    delegated.Id,
                    delegated.Name,
                    delegatedSummary.TotalScore,
                    delegatedSummary.Rank,
                    ranked.Count,
                    delegatedSummary.TotalObjectives - delegatedSummary.CompletedCount - delegatedSummary.FailedCount,
                    delegatedSummary.TotalObjectives,
                    activity.At,
                    activity.Type,
                    delegatedSummary.FailedCount,
                    GetCompletionStatus(delegatedSummary)));
            }

            if (row.CreatedById == userId)
            {
                var activity = ranked
                    .Select(s => GetLastActivity(row.Id, s, lastInfoActivity))
                    .Where(item => item.At.HasValue)
                    .MaxBy(item => item.At)
                    ?? new ActivityRow(null, null);
                ownedEvents.Add(new MyOwnedEventResponse(
                    row.Id,
                    row.Name,
                    row.UrlAlias,
                    status,
                    ranked.Count,
                    activity.At,
                    activity.Type));
            }
        }

        var quickCompleteEnabled = await db.FeatureFlags
            .Where(flag => flag.Key == FeatureFlagKeys.MyEventsQuickComplete)
            .Select(flag => flag.Enabled)
            .SingleOrDefaultAsync(ct);

        return Results.Ok(new MyEventsResponse(
            competitorEvents,
            delegatedEvents,
            ownedEvents,
            quickCompleteEnabled));
    }

    internal static async Task<IResult> GetObjectives(
        Guid eventId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct,
        Guid? competitorId = null)
    {
        var userId = EventOwnership.GetUserId(principal);
        var targetId = competitorId ?? userId;
        var eventExists = await db.Events
            .IgnoreQueryFilters()
            .AnyAsync(ev => ev.Id == eventId, ct);
        if (!eventExists)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        var isCompetitor = await db.EventCompetitors
            .AnyAsync(competitor => competitor.EventId == eventId && competitor.UserId == targetId, ct);
        if (!isCompetitor)
            return targetId == userId
                ? Results.Problem(
                    detail: "You are not a competitor in this event.",
                    statusCode: StatusCodes.Status403Forbidden)
                : Results.Problem(
                    detail: "Competitor not found.",
                    statusCode: StatusCodes.Status404NotFound);

        if (targetId != userId)
        {
            var isDelegate = await db.EventCompetitorModerators.AnyAsync(
                moderator =>
                    moderator.EventId == eventId
                    && moderator.CompetitorUserId == targetId
                    && moderator.ModeratorUserId == userId,
                ct);
            if (!isDelegate)
                return Results.Problem(
                    detail: "You are not delegated to this competitor.",
                    statusCode: StatusCodes.Status403Forbidden);
        }
        var scoreboard = await ScoreboardEndpoint.BuildAsync(eventId, db, ct, includeArchived: true, enabledGamesOnly: true);
        var entry = scoreboard?.Entries.FirstOrDefault(item => item.UserId == targetId);
        if (entry is null)
            return Results.Problem(detail: "Competitor not found.", statusCode: StatusCodes.Status404NotFound);

        return Results.Ok(new MyEventObjectivesResponse(
            entry.UserId,
            entry.DisplayName,
            entry.Games.Select(game => new MyEventGameResponse(
                game.EventGameId,
                game.GameName,
                game.Objectives.Select(objective => new MyEventObjectiveResponse(
                    objective.ObjectiveId,
                    objective.Name,
                    objective.IsCompleted,
                    objective.CompletedAt,
                    objective.Score,
                    objective.IsFailed,
                    objective.FailedAt,
                    objective.Category)).ToList(),
                game.IsTrialActive,
                game.HasTrialRun)).ToList()));
    }

    /// <summary>
    /// "Stopped" versus "upcoming" comes from <c>Event.StoppedAt</c>, not from
    /// the audit trail: audit rows are subject to retention, and reading the
    /// lifecycle out of them turned every finished event back into an upcoming
    /// one once its rows aged out.
    /// </summary>
    private static string GetStatus(MyEventRow row)
    {
        if (row.IsArchived) return ArchivedStatus;
        if (row.IsStarted) return LiveStatus;
        return row.HasBeenStopped ? StoppedStatus : UpcomingStatus;
    }

    private static string GetCompletionStatus(CompetitorSummary summary) =>
        ObjectiveOutcomeCalculator.ForCompetitor(
            summary.TotalObjectives, summary.CompletedCount, summary.FailedCount).ToString();

    private static ActivityRow GetLastActivity(
        Guid eventId,
        CompetitorSummary summary,
        IReadOnlyDictionary<(Guid EventId, Guid UserId), (DateTime CreatedAt, string Type)> lastInfoActivity)
    {
        var completion = new ActivityRow(summary.LastCompletedAt, ObjectiveCompletedActivity);
        if (!lastInfoActivity.TryGetValue((eventId, summary.UserId), out var info))
            return completion;
        // Nullable comparison: when completion.At is null this is always false,
        // so a competitor with no completions falls through to their info-based
        // activity below.
        if (completion.At >= info.CreatedAt) return completion;

        var type = string.Equals(info.Type, nameof(EventGameCompetitorInfoType.DeathClip), StringComparison.Ordinal)
            ? ClipAddedActivity
            : NoteAddedActivity;
        return new ActivityRow(info.CreatedAt, type);
    }

    private sealed record MyEventRow(
        Guid Id,
        string Name,
        string? UrlAlias,
        bool IsArchived,
        bool IsStarted,
        bool HasBeenStopped,
        Guid CreatedById,
        TieBreakMode TieBreakMode,
        Guid CompetitorId,
        List<DelegatedCompetitorRow> DelegatedCompetitors);

    private sealed record DelegatedCompetitorRow(Guid Id, string Name);
    private sealed record ActivityRow(DateTime? At, string? Type);
}
