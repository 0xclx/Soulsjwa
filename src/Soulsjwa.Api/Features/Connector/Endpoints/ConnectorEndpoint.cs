using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Api.Features.Games.Services;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Shared;

namespace Soulsjwa.Api.Features.Connector.Endpoints;

/// <summary>
/// What the desktop connector talks to. Response records live in
/// <c>Soulsjwa.Shared</c> (see <c>ConnectorContracts.cs</c>) so the connector
/// deserializes the very types these handlers serialize. Handlers are <c>internal</c> rather than
/// <c>private</c> so <c>Soulsjwa.IntegrationTests</c> can invoke them directly —
/// see <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class ConnectorEndpoint : IEndpoint
{
    /// <summary>
    /// Transport-level override of Program.cs's 1 MiB global Kestrel request-body
    /// limit, sized just above ConnectorSubmissionValidator.MaxDataBytes to leave
    /// room for the JSON envelope around the Data property.
    /// </summary>
    private const int MaxSubmitRequestBytes = ConnectorSubmissionValidator.MaxDataBytes + 4 * 1024;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/connector");

        group.MapGet("/version", GetRequiredVersion)
            .WithName("GetConnectorVersion")
            .WithSummary("Returns the required connector version")
            .Produces<ConnectorVersionResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        group.MapGet("/games/{gameId:int}/data", GetGameData)
            .WithName("GetConnectorGameData")
            .WithSummary("Returns data point definitions (offset, dataType) for a connector-supported game. This data is defined in C# and not stored in the database.")
            .Produces<ConnectorGameDataResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPost("/events/{eventId:guid}/games/{eventGameId:guid}/submit", SubmitGameData)
            .WithName("SubmitConnectorGameData")
            .WithSummary("Submits game state data for a specific event-game; evaluates rules and auto-completes matching objectives")
            .Produces<ConnectorDataSubmissionResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .WithMetadata(new RequestSizeLimitAttribute(MaxSubmitRequestBytes))
            .RequireRateLimiting(RateLimitPolicies.Connector)
            .RequireAuthorization();

        group.MapGet("/supported-games", GetSupportedGames)
            .WithName("GetConnectorSupportedGames")
            .WithSummary("Returns all connector-supported games")
            .Produces<List<ConnectorSupportedGameResponse>>(StatusCodes.Status200OK)
            .AllowAnonymous();

        group.MapGet("/events", GetEvents)
            .WithName("GetConnectorEvents")
            .WithSummary("Returns the events the authenticated user competes in, with their games, for the connector's event/game picker")
            .Produces<List<ConnectorEventResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }

    private static IResult GetRequiredVersion()
    {
        return Results.Ok(new ConnectorVersionResponse(ConnectorConstants.Version));
    }

    internal static async Task<IResult> GetGameData(
        int gameId,
        AppDbContext db,
        CancellationToken ct)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct);
        if (game is null) return Results.Problem(detail: "Game not found.", statusCode: StatusCodes.Status404NotFound);
        if (!game.ConnectorSupported) return Results.Problem(detail: "Game is not connector-supported.", statusCode: StatusCodes.Status404NotFound);

        var dataPoints = GameDataDefinitions.ForGame(gameId);
        if (dataPoints.Count == 0) return Results.Problem(detail: "No data definitions found for this game.", statusCode: StatusCodes.Status404NotFound);

        return Results.Ok(new ConnectorGameDataResponse(game.Id, game.Name, dataPoints.ToList()));
    }

    internal static async Task<IResult> SubmitGameData(
        Guid eventId,
        Guid eventGameId,
        ConnectorSubmissionPayload submission,
        ClaimsPrincipal principal,
        AppDbContext db,
        IOutputCacheStore cache,
        ILogger<ConnectorEndpoint> logger,
        CancellationToken ct)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.ConnectorSubmitGameData,
            DiagnosticsConfig.ActivityNames.ConnectorSubmitGameData);
        operation.Activity?.SetTag(DiagnosticsConfig.Tags.UserId, EventOwnership.GetUserId(principal));

        // The submitting user is the API key owner — never trust an id in the body.
        var userId = EventOwnership.GetUserId(principal);

        var (_, gateError) = await EventContext.RequireRunningEventAsync(eventId, db, ct);
        if (gateError is not null)
        {
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, "Event not found or not started.");
            return gateError;
        }

        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);

        if (eventGame is null)
        {
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, "Game is not part of this event.");
            return Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound);
        }

        var isCompetitor = await db.EventCompetitors
            .AnyAsync(ec => ec.EventId == eventId && ec.UserId == userId, ct);

        if (!isCompetitor)
        {
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, "User is not a competitor in this event.");
            return Results.Problem(
                detail: "User is not a competitor in this event.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        // Rejecting a malformed payload here (400) rather than letting rule
        // evaluation silently resolve garbage to "false" gives connector authors
        // a precise, actionable error. The parsed document is handed back so the
        // rest of this request doesn't re-parse the same payload.
        var validationErrors = ConnectorSubmissionValidator.Validate(submission.Data, eventGame.KnownGameId, out var submissionDocument);
        if (validationErrors.Count > 0)
        {
            operation.SetError(DiagnosticsConfig.OperationStatuses.InvalidArgument, "Submission data failed validation.");
            return Results.ValidationProblem(validationErrors);
        }
        using var submissionDocumentScope = submissionDocument;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var objectives = await db.Objectives
            .Where(o => o.EventGameId == eventGame.Id && (o.Rule != null || o.FailRule != null))
            .ToListAsync(ct);
        var objectiveIds = objectives.Select(o => o.Id).ToList();
        await ObjectiveOutcomeLock.AcquireAsync(db, eventGame.Id, ct);

        // Trial runs work whether or not the game is the event's active game —
        // that's the point, training between matches — so the IsEnabled gate is
        // skipped while this competitor has one recording for this game. Must be
        // resolved here, inside the transaction and under the lock, against the
        // same target the inserts below use: doing it before the lock left a
        // window for a run to start mid-submission, landing the whole batch
        // officially on a possibly-disabled game and cascading fail rules onto
        // other competitors off practice progress.
        var target = await TrialRunLookup.ResolveWriteTargetAsync(db, eventGame.Id, userId, ct);
        if (target.IsBlocked)
        {
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, "Trial mode is enabled but not recording.");
            return Results.Problem(
                detail: TrialRunLookup.BlockedDetail, statusCode: StatusCodes.Status409Conflict);
        }
        var trialRunId = target.TrialRunId;
        if (!eventGame.IsEnabled && trialRunId is null)
        {
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, "Game is not enabled.");
            return Results.Problem(detail: "Game is not enabled.", statusCode: StatusCodes.Status403Forbidden);
        }

        // Scoped to this submission's own context (official, or this specific
        // trial run) — an objective already resolved officially is still
        // pending within a trial, and vice versa.
        var completedObjectiveIds = await db.CompletedObjectives
            .Where(co => co.UserId == userId && objectiveIds.Contains(co.ObjectiveId) && co.TrialRunId == trialRunId)
            .Select(co => co.ObjectiveId)
            .ToListAsync(ct);
        var failedObjectiveIds = await db.FailedObjectives
            .Where(f => f.UserId == userId && objectiveIds.Contains(f.ObjectiveId) && f.TrialRunId == trialRunId)
            .Select(f => f.ObjectiveId)
            .ToListAsync(ct);

        var pendingObjectives = objectives
            .Where(o => !completedObjectiveIds.Contains(o.Id) && !failedObjectiveIds.Contains(o.Id))
            .ToList();

        // The `competitorCompletions` variable fail rules may reference: how many
        // OTHER event competitors have already completed the objective.
        var failRuleObjectiveIds = pendingObjectives
            .Where(o => o.FailRule != null)
            .Select(o => o.Id)
            .ToList();
        // Always the OFFICIAL count, regardless of whether this submission
        // itself is official or a trial — a fail rule's `competitorCompletions`
        // reflects real competition standing either way.
        var otherCompletionCounts = failRuleObjectiveIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.CompletedObjectives
                .Where(co => failRuleObjectiveIds.Contains(co.ObjectiveId)
                    && co.UserId != userId
                    && co.TrialRunId == null
                    && db.EventCompetitors.Any(ec =>
                        ec.EventId == eventId && ec.UserId == co.UserId))
                .GroupBy(co => co.ObjectiveId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var newlyCompleted = 0;
        var newlyFailed = 0;
        var completedObjectiveIdsThisSubmission = new List<Guid>();

        // Stored on each completion/failure: the scoreboard uses it as a
        // tie-breaker independent of real-world submission time.
        var inGameTimeMs = ConnectorIngameTime.ExtractMilliseconds(submissionDocument!, eventGame.KnownGameId);

        // Parsed once for the whole request: the submission payload as a
        // Newtonsoft JObject (JsonLogic.Net's input type), instead of re-parsing
        // it on every loop iteration. Rules come from the process-wide parsed
        // cache — every submission of every competitor evaluates the same
        // objectives, so re-parsing them per request was pure waste.
        var submissionData = JObject.Parse(submission.Data!);
        var rulesEvaluated = 0;

        foreach (var objective in pendingObjectives)
        {
            // A completion match always wins over a failure match in the same
            // submission — an objective is never marked both at once.
            if (objective.Rule is not null)
            {
                rulesEvaluated++;
                if (RuleEvaluator.Evaluate(RuleJson.ParsedRuleCache.Get(objective.Rule), submissionData, competitorCompletions: null))
                {
                    db.CompletedObjectives.Add(new Events.Entities.CompletedObjective
                    {
                        ObjectiveId = objective.Id,
                        UserId = userId,
                        CompletedAt = DateTime.UtcNow,
                        InGameTimeMs = inGameTimeMs,
                        TrialRunId = trialRunId,
                    });
                    newlyCompleted++;
                    completedObjectiveIdsThisSubmission.Add(objective.Id);
                    continue;
                }
            }

            if (objective.FailRule is null) continue;

            var otherCompletions = otherCompletionCounts.GetValueOrDefault(objective.Id);
            rulesEvaluated++;
            if (RuleEvaluator.Evaluate(RuleJson.ParsedRuleCache.Get(objective.FailRule), submissionData, otherCompletions))
            {
                db.FailedObjectives.Add(new Events.Entities.FailedObjective
                {
                    ObjectiveId = objective.Id,
                    UserId = userId,
                    FailedAt = DateTime.UtcNow,
                    InGameTimeMs = inGameTimeMs,
                    TrialRunId = trialRunId,
                });
                newlyFailed++;
            }
        }

        if (newlyCompleted > 0 || newlyFailed > 0)
        {
            await db.SaveChangesAsync(ct);

            // An OFFICIAL completion changes `competitorCompletions` for every
            // other pending competitor on that objective, so resolve their
            // count-only fail rules now rather than at their next submission.
            // A trial completion is private practice and must never cascade.
            if (trialRunId is null)
                await FailRuleCascadeEvaluator.ApplyAsync(db, completedObjectiveIdsThisSubmission, userId, ct);
        }

        await transaction.CommitAsync(ct);
        // Only a changed row invalidates the scoreboard: connectors submit
        // every couple of seconds and almost every submission changes nothing.
        if (newlyCompleted > 0 || newlyFailed > 0)
            await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        operation.SetTag(DiagnosticsConfig.Tags.RulesEvaluated, rulesEvaluated);

        logger.ConnectorSubmissionProcessed(eventId, eventGameId, userId, newlyCompleted, newlyFailed);

        return Results.Ok(new ConnectorDataSubmissionResult(newlyCompleted, newlyFailed));
    }

    internal static async Task<IResult> GetSupportedGames(
        AppDbContext db,
        CancellationToken ct)
    {
        var games = await db.Games
            .Where(g => g.ConnectorSupported)
            .OrderBy(g => g.Name)
            .Select(g => new ConnectorSupportedGameResponse(
                g.Id, g.Name, g.RequiredConnectorVersion))
            .ToListAsync(ct);

        return Results.Ok(games);
    }

    /// <summary>
    /// The events this user can actually submit to — only those they compete
    /// in, since <see cref="SubmitGameData"/> refuses everyone else — each with
    /// its full game list. The public events list deliberately carries no games
    /// (it is a list-card shape), which is why the connector has its own route
    /// rather than reading that one. Archived events are hidden by the global
    /// query filter; not-started events are listed with <c>isStarted=false</c>
    /// so the connector can explain why submissions are refused.
    /// </summary>
    internal static async Task<IResult> GetEvents(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = EventOwnership.GetUserId(principal);

        var events = await db.Events
            .Where(e => e.Competitors.Any(c => c.UserId == userId))
            .OrderBy(e => e.Name)
            .Select(e => new ConnectorEventResponse(
                e.Id,
                e.Name,
                e.Description,
                e.IsStarted,
                e.AllowTrialRuns,
                e.EventGames
                    .OrderBy(eg => eg.SortOrder)
                    .Select(eg => new ConnectorEventGameResponse(
                        eg.Id,
                        eg.KnownGameId,
                        eg.CustomGameName ?? eg.KnownGame!.Name,
                        eg.KnownGame != null ? eg.KnownGame.Name : null,
                        eg.KnownGame != null && eg.KnownGame.ConnectorSupported,
                        eg.KnownGame != null ? eg.KnownGame.RequiredConnectorVersion : null,
                        eg.IsEnabled))
                    .ToList()))
            .ToListAsync(ct);

        return Results.Ok(events);
    }
}
