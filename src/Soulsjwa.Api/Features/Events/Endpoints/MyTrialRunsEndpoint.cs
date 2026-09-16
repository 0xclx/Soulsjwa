using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events.Endpoints;

/// <summary>
/// One trial run the caller may see, with the progress recorded under it.
/// <see cref="TotalObjectives"/> counts the game's objectives so a list row
/// can show progress without fetching the objective list.
/// </summary>
public sealed record MyTrialRunResponse(
    Guid TrialRunId,
    Guid EventId,
    string EventName,
    string? UrlAlias,
    Guid EventGameId,
    string GameName,
    bool IsGameEnabled,
    Guid CompetitorId,
    string CompetitorName,
    bool IsOwnTrial,
    string State,
    DateTime? StartedAt,
    int Score,
    int CompletedCount,
    int FailedCount,
    int TotalObjectives,
    DateTime? LastCompletedAt);

/// <summary>
/// The trial-run surface behind My Events' Trial tab. Trial
/// progress is reported only here and on the scoreboard/overlay — the regular
/// objectives endpoint stays official-only, so the two can never be confused
/// for one another in the same view.
///
/// Access follows the same rule as editing a competitor's info: an admin, the
/// event owner, the competitor themselves, or a moderator they delegated.
///
/// That rule scopes *this* endpoint — which run ids a caller may address, and
/// whose runs the list enumerates — and is not a confidentiality guarantee
/// about the figures. The same trial score, counts, last-completed and
/// per-objective trial timestamps are served anonymously on
/// <c>GET /events/{id}/scoreboard</c>, by design: a trial run is watchable
/// practice, shown in amber behind a Trial badge. Anyone reasoning about
/// hiding trial progress has to change the scoreboard payload, not this.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="CompletedObjectivesEndpoint"/> for why.
/// </summary>
public class MyTrialRunsEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/me/trial-runs").RequireAuthorization();

        group.MapGet("/", List)
            .WithName("GetMyTrialRuns")
            .WithSummary("Gets the trial runs the current user may see, with their progress")
            .Produces<List<MyTrialRunResponse>>();

        group.MapGet("/{trialRunId:guid}/objectives", GetObjectives)
            .WithName("GetMyTrialRunObjectives")
            .WithSummary("Gets a trial run's own objective completions and failures")
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
        var isAdmin = EventOwnership.IsAdmin(principal);

        // Archived events are excluded here rather than by the global filter,
        // which the navigation below would otherwise apply inconsistently.
        var visible = db.TrialRuns
            .IgnoreQueryFilters()
            .Where(t => !t.Event.IsArchived);

        // Branched rather than ORed with a captured bool: a parameter in the
        // first branch of an OR cannot be proved false at plan time, so Postgres
        // would refuse IX_TrialRuns_UserId and sequentially scan every trial run
        // in the system — on an endpoint polled every 5s. Delegation goes
        // through the competitor navigation rather than a DbSet subquery: the
        // navigation is already scoped to this event and competitor, so only the
        // moderator needs checking.
        if (!isAdmin)
        {
            visible = visible.Where(t =>
                t.UserId == userId
                || t.Event.CreatedById == userId
                || t.Event.Competitors.Any(c =>
                    c.UserId == t.UserId
                    && c.Moderators.Any(m => m.ModeratorUserId == userId)));
        }

        var runs = await visible
            .Select(t => new
            {
                t.Id,
                t.EventId,
                EventName = t.Event.Name,
                t.Event.UrlAlias,
                t.EventGameId,
                GameName = t.EventGame.CustomGameName ?? t.EventGame.KnownGame!.Name,
                IsGameEnabled = t.EventGame.IsEnabled,
                t.UserId,
                CompetitorName = t.User.DisplayName,
                t.State,
                t.StartedAt,
                TotalObjectives = t.EventGame.Objectives.Count,
            })
            .ToListAsync(ct);

        if (runs.Count == 0) return Results.Ok(new List<MyTrialRunResponse>());

        var runIds = runs.Select(r => r.Id).ToList();
        var completions = await db.CompletedObjectives
            .Where(co => co.TrialRunId != null && runIds.Contains(co.TrialRunId.Value))
            .Select(co => new { TrialRunId = co.TrialRunId!.Value, co.CompletedAt, co.Objective.Score })
            .ToListAsync(ct);
        var failureCounts = await db.FailedObjectives
            .Where(f => f.TrialRunId != null && runIds.Contains(f.TrialRunId.Value))
            .GroupBy(f => f.TrialRunId!.Value)
            .Select(g => new { TrialRunId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TrialRunId, x => x.Count, ct);

        var progressByRun = completions
            .GroupBy(c => c.TrialRunId)
            .ToDictionary(
                g => g.Key,
                g => (Score: g.Sum(c => c.Score), Count: g.Count(), Last: g.Max(c => (DateTime?)c.CompletedAt)));

        return Results.Ok(runs
            .Select(r =>
            {
                var progress = progressByRun.GetValueOrDefault(r.Id);
                return new MyTrialRunResponse(
                    r.Id,
                    r.EventId,
                    r.EventName,
                    r.UrlAlias,
                    r.EventGameId,
                    r.GameName ?? "Unknown",
                    r.IsGameEnabled,
                    r.UserId,
                    r.CompetitorName,
                    r.UserId == userId,
                    r.State.ToString(),
                    r.StartedAt,
                    progress.Score,
                    progress.Count,
                    failureCounts.GetValueOrDefault(r.Id),
                    r.TotalObjectives,
                    progress.Last);
            })
            .OrderByDescending(r => r.IsOwnTrial)
            .ThenBy(r => r.EventName)
            .ThenBy(r => r.GameName)
            .ToList());
    }

    internal static async Task<IResult> GetObjectives(
        Guid trialRunId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var trialRun = await db.TrialRuns
            .IgnoreQueryFilters()
            .Include(t => t.Event)
            .Include(t => t.User)
            .Include(t => t.EventGame).ThenInclude(eg => eg.KnownGame)
            .Include(t => t.EventGame).ThenInclude(eg => eg.Objectives)
            .FirstOrDefaultAsync(t => t.Id == trialRunId, ct);
        if (trialRun is null)
            return Results.Problem(detail: "Trial run not found.", statusCode: StatusCodes.Status404NotFound);

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(
                trialRun.Event, principal, trialRun.UserId, db, ct) is { } authError)
            return authError;

        var completions = await db.CompletedObjectives
            .Where(co => co.TrialRunId == trialRunId)
            .Select(co => new { co.ObjectiveId, co.CompletedAt })
            .ToDictionaryAsync(x => x.ObjectiveId, x => x.CompletedAt, ct);
        var failures = await db.FailedObjectives
            .Where(f => f.TrialRunId == trialRunId)
            .Select(f => new { f.ObjectiveId, f.FailedAt })
            .ToDictionaryAsync(x => x.ObjectiveId, x => x.FailedAt, ct);

        // Deliberately the same shape as GET /me/events/{id}/objectives, but
        // filled from this run's own rows: the Trial tab reuses the regular
        // objective list, and a completion here is a completion of the trial.
        var objectives = trialRun.EventGame.Objectives
            .OrderBy(o => o.Name)
            .Select(o =>
            {
                var completedAt = completions.TryGetValue(o.Id, out var at) ? at : (DateTime?)null;
                var failedAt = completedAt is null && failures.TryGetValue(o.Id, out var failed)
                    ? failed
                    : (DateTime?)null;
                return new MyEventObjectiveResponse(
                    o.Id, o.Name, completedAt is not null, completedAt, o.Score,
                    failedAt is not null, failedAt, o.Category);
            })
            .ToList();

        return Results.Ok(new MyEventObjectivesResponse(
            trialRun.UserId,
            trialRun.User.DisplayName,
            [
                new MyEventGameResponse(
                    trialRun.EventGameId,
                    trialRun.EventGame.CustomGameName ?? trialRun.EventGame.KnownGame?.Name ?? "Unknown",
                    objectives),
            ]));
    }
}
