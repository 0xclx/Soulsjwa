using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Services;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record CreateObjectiveRequest(string Name, int Score, string? Category, string? Metadata, string? Rule, string? FailRule = null);
public sealed record CreatePredefinedObjectiveRequest(int GameId, string Name, int Score, string? Category, string? Metadata, string? Rule, string? FailRule = null);
public sealed class PatchObjectiveRequest
{
    private string? _category;
    private string? _rule;
    private string? _failRule;

    public string? Name { get; init; }
    public int? Score { get; init; }
    public string? Category
    {
        get => _category;
        init
        {
            _category = value;
            HasCategory = true;
        }
    }
    public string? Metadata { get; init; }
    public string? Rule
    {
        get => _rule;
        init
        {
            _rule = value;
            HasRule = true;
        }
    }
    public string? FailRule
    {
        get => _failRule;
        init
        {
            _failRule = value;
            HasFailRule = true;
        }
    }

    [JsonIgnore]
    public bool HasCategory { get; private init; }

    [JsonIgnore]
    public bool HasRule { get; private init; }

    [JsonIgnore]
    public bool HasFailRule { get; private init; }
}
public sealed record AssignPredefinedObjectiveRequest(Guid ObjectiveId);
public sealed record ImportPredefinedObjectivesResult(int ImportedCount, int SkippedCount);
public sealed record PredefinedObjectiveResponse(
    Guid Id,
    int GameId,
    string Name,
    int Score,
    string? Category,
    string? Metadata,
    string? Rule,
    string? FailRule = null);

/// <summary>
/// Optional filter for bulk import: null or empty imports every predefined
/// objective for the game, otherwise only the listed ids.
/// </summary>
public sealed record ImportPredefinedObjectivesRequest(List<Guid>? ObjectiveIds = null);

/// <summary>
/// The objective catalogue: predefined objectives per game, an event-game's
/// own objectives, and importing the former into the latter.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="CompletedObjectivesEndpoint"/> for why. The API suite keeps the
/// route, authorization and output-caching contract.
/// </summary>
public class ObjectivesEndpoint : IEndpoint
{
    private const int MaxNameLength = 200;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/objectives/predefined", ListPredefined)
            .WithName("ListPredefinedObjectives")
            .WithSummary("Lists predefined objective templates, optionally filtered by game")
            .Produces<List<ObjectiveResponse>>(StatusCodes.Status200OK)
            .CacheOutput("PredefinedObjectives")
            .AllowAnonymous();

        app.MapPost(ApiRoutes.Prefix + "/objectives/predefined", CreatePredefined)
            .WithName("CreatePredefinedObjective")
            .WithSummary("Creates a predefined objective template")
            .Produces<PredefinedObjectiveResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAdmin();

        var group = app.MapGroup(ApiRoutes.Prefix + "/events/{eventId:guid}/games/{eventGameId:guid}/objectives");

        group.MapGet("/", ListObjectives)
            .WithName("ListEventGameObjectives")
            .WithSummary("Lists objectives for a game in an event")
            .Produces<List<ObjectiveResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/", CreateObjective)
            .WithName("CreateEventGameObjective")
            .WithSummary("Creates a custom objective for a game in an event (owner only)")
            .Produces<ObjectiveResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPost("/assign", AssignPredefined)
            .WithName("AssignPredefinedObjective")
            .WithSummary("Assigns a predefined objective to a game in an event (owner only)")
            .Produces<ObjectiveResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPost("/import-predefined", ImportAllPredefined)
            .WithName("ImportAllPredefinedObjectives")
            .WithSummary("Imports predefined objectives for the game into this event (owner only). When the body's objectiveIds is null/empty, ALL predefined objectives are imported; otherwise only the specified subset. Objectives already imported (same name) are skipped.")
            .Produces<ImportPredefinedObjectivesResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPatch("/{objectiveId:guid}", PatchObjective)
            .WithName("PatchEventGameObjective")
            .WithSummary("Updates an objective (owner only)")
            .Produces<ObjectiveResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapDelete("/{objectiveId:guid}", DeleteObjective)
            .WithName("DeleteEventGameObjective")
            .WithSummary("Deletes an objective and its completions (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();
    }

    /// <summary>
    /// Caps the unfiltered (no gameId) catalog fetch. Requiring gameId or
    /// paginating would break the admin catalog table (CatalogTab.tsx), which
    /// deliberately fetches the whole catalog once and searches/sorts/paginates
    /// it client-side. The catalog is hand-curated and around a thousand rows
    /// (Elden Ring alone is several hundred), so a cap bounds the response
    /// without changing behaviour.
    /// </summary>
    private const int MaxUnfilteredPredefinedResults = 2000;

    /// <summary>
    /// Every path that adds an objective to an event-game appends it after the
    /// existing ones, so the display order is stable whether the objective was
    /// typed in, assigned from the catalog, or imported in bulk.
    /// </summary>
    private static async Task<int> NextSortOrderAsync(AppDbContext db, Guid eventGameId, CancellationToken ct) =>
        (await db.Objectives
            .Where(o => o.EventGameId == eventGameId)
            .Select(o => (int?)o.SortOrder)
            .MaxAsync(ct) ?? -1) + 1;

    private static string ImportIdentity(string name, string? rule) =>
        rule is null ? "name:" + name : "rule:" + rule;

    internal static async Task<IResult> ListPredefined(AppDbContext db, int? gameId, CancellationToken ct)
    {
        var query = db.Objectives
            .AsNoTracking()
            .Where(o => o.IsPredefined && o.EventGameId == null && o.GameId.HasValue);

        if (gameId.HasValue)
            query = query.Where(o => o.GameId == gameId.Value);

        query = query.OrderBy(o => o.GameId).ThenBy(o => o.Name);

        if (!gameId.HasValue)
            query = query.Take(MaxUnfilteredPredefinedResults);

        var objectives = await query
            .Select(o => new PredefinedObjectiveResponse(
                o.Id, o.GameId!.Value, o.Name, o.Score, o.Category, o.Metadata, o.Rule, o.FailRule))
            .ToListAsync(ct);

        return Results.Ok(objectives);
    }

    internal static async Task<IResult> CreatePredefined(
        CreatePredefinedObjectiveRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<ObjectivesEndpoint> logger,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var errors = ValidateObjectiveRequest(request.Name, request.Score, request.Metadata, request.Rule, request.FailRule);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        if (!await db.Games.AnyAsync(g => g.Id == request.GameId, ct))
            return Results.Problem(detail: "Game not found.", statusCode: StatusCodes.Status404NotFound);

        var objective = new Objective
        {
            GameId = request.GameId,
            Name = request.Name.Trim(),
            Score = request.Score,
            Category = request.Category,
            Metadata = request.Metadata,
            Rule = request.Rule,
            FailRule = request.FailRule,
            IsPredefined = true,
            EventGameId = null
        };

        db.Objectives.Add(objective);
        audit.Log(db, AuditEventTypes.ObjectivePredefinedCreated, EventOwnership.GetUserId(principal),
            objectiveId: objective.Id,
            after: new { objective.GameId, objective.Name, objective.Score, objective.Category });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.InvalidTextRepresentation })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Metadata"] = ["One or more of Metadata, Rule, or FailRule is not valid JSON."],
            });
        }
        await cache.EvictByTagAsync(CacheTags.PredefinedObjectives(request.GameId), ct);
        await cache.EvictByTagAsync(CacheTags.PredefinedObjectivesAll, ct);
        logger.PredefinedObjectiveCreated(objective.Id);

        return Results.Created($"/api/v1/objectives/predefined",
            new PredefinedObjectiveResponse(
                objective.Id, objective.GameId.Value, objective.Name, objective.Score, objective.Category, objective.Metadata, objective.Rule, objective.FailRule));
    }

    internal static async Task<IResult> ListObjectives(
        Guid eventId,
        Guid eventGameId,
        AppDbContext db,
        CancellationToken ct)
    {
        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);

        if (eventGame is null)
            return Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound);

        var objectives = await db.Objectives
            .Where(o => o.EventGameId == eventGame.Id)
            .Select(o => new ObjectiveResponse(o.Id, o.Name, o.Score, o.Category, o.Metadata, o.IsPredefined, o.Rule, o.FailRule))
            .ToListAsync(ct);

        return Results.Ok(objectives);
    }

    internal static async Task<IResult> CreateObjective(
        Guid eventId,
        Guid eventGameId,
        CreateObjectiveRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<ObjectivesEndpoint> logger,
        CancellationToken ct)
    {
        var errors = ValidateObjectiveRequest(request.Name, request.Score, request.Metadata, request.Rule, request.FailRule);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var eventGame = await db.EventGames
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);

        if (eventGame is null)
            return Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(eventGame.Event, principal, "create objectives") is { } ownerError)
            return ownerError;

        var nextSortOrder = await NextSortOrderAsync(db, eventGame.Id, ct);

        var objective = new Objective
        {
            EventGameId = eventGame.Id,
            Name = request.Name.Trim(),
            Score = request.Score,
            Category = request.Category,
            Metadata = request.Metadata,
            Rule = request.Rule,
            FailRule = request.FailRule,
            IsPredefined = false,
            SortOrder = nextSortOrder
        };

        db.Objectives.Add(objective);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.ObjectiveCreated, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objective.Id,
            after: new { objective.Name, objective.Score, objective.Category, objective.Metadata, objective.Rule, objective.FailRule, objective.IsPredefined });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.InvalidTextRepresentation })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Metadata"] = ["One or more of Metadata, Rule, or FailRule is not valid JSON."],
            });
        }
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectiveCreated(objective.Id, eventGameId, eventId, callerId);

        return Results.Created($"/api/v1/events/{eventId}/games/{eventGameId}/objectives/{objective.Id}",
            new ObjectiveResponse(objective.Id, objective.Name, objective.Score, objective.Category, objective.Metadata, objective.IsPredefined, objective.Rule, objective.FailRule));
    }

    internal static async Task<IResult> AssignPredefined(
        Guid eventId,
        Guid eventGameId,
        AssignPredefinedObjectiveRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<ObjectivesEndpoint> logger,
        CancellationToken ct)
    {
        var eventGame = await db.EventGames
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);

        if (eventGame is null)
            return Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(eventGame.Event, principal, "assign objectives") is { } ownerError)
            return ownerError;

        var predefined = await db.Objectives
            .FirstOrDefaultAsync(o => o.Id == request.ObjectiveId && o.IsPredefined && o.EventGameId == null, ct);

        if (predefined is null)
            return Results.Problem(detail: "Predefined objective not found.", statusCode: StatusCodes.Status404NotFound);

        var objective = new Objective
        {
            EventGameId = eventGame.Id,
            Name = predefined.Name,
            Score = predefined.Score,
            Category = predefined.Category,
            Metadata = predefined.Metadata,
            Rule = predefined.Rule,
            FailRule = predefined.FailRule,
            GameId = predefined.GameId,
            IsPredefined = true,
            SortOrder = await NextSortOrderAsync(db, eventGame.Id, ct),
        };

        db.Objectives.Add(objective);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.ObjectivePredefinedAssigned, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objective.Id,
            after: new { PredefinedObjectiveId = request.ObjectiveId, objective.Name, objective.Score });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.PredefinedObjectiveAssigned(
            request.ObjectiveId,
            objective.Id,
            eventGameId,
            eventId,
            callerId);

        return Results.Created($"/api/v1/events/{eventId}/games/{eventGameId}/objectives/{objective.Id}",
            new ObjectiveResponse(objective.Id, objective.Name, objective.Score, objective.Category, objective.Metadata, objective.IsPredefined, objective.Rule, objective.FailRule));
    }

    internal static async Task<IResult> PatchObjective(
        Guid eventId,
        Guid eventGameId,
        Guid objectiveId,
        PatchObjectiveRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<ObjectivesEndpoint> logger,
        CancellationToken ct)
    {
        var eventGame = await db.EventGames
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);

        if (eventGame is null)
            return Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(eventGame.Event, principal, "update objectives") is { } ownerError)
            return ownerError;

        var objective = await db.Objectives
            .FirstOrDefaultAsync(o => o.Id == objectiveId && o.EventGameId == eventGame.Id, ct);

        if (objective is null)
            return Results.Problem(detail: "Objective not found.", statusCode: StatusCodes.Status404NotFound);

        var before = new { objective.Name, objective.Score, objective.Category, objective.Metadata, objective.Rule, objective.FailRule };

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > MaxNameLength)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = [$"Name is required and must be {MaxNameLength} characters or fewer."]
                });
            objective.Name = request.Name.Trim();
        }

        if (request.Score is not null)
        {
            if (request.Score.Value < 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Score"] = ["Score must be non-negative."]
                });
            objective.Score = request.Score.Value;
        }

        if (request.HasCategory)
            objective.Category = request.Category;

        var patchErrors = new Dictionary<string, string[]>();
        if (request.Metadata is not null)
        {
            if (ObjectiveRuleValidator.ValidateMetadata(request.Metadata) is { } metadataError)
                patchErrors["Metadata"] = [metadataError];
            else
                objective.Metadata = request.Metadata;
        }

        if (request.HasRule)
        {
            if (ObjectiveRuleValidator.ValidateRule(request.Rule) is { } ruleError)
                patchErrors["Rule"] = [ruleError];
            else
                objective.Rule = request.Rule;
        }

        if (request.HasFailRule)
        {
            if (ObjectiveRuleValidator.ValidateRule(request.FailRule) is { } failRuleError)
                patchErrors["FailRule"] = [failRuleError];
            else
                objective.FailRule = request.FailRule;
        }

        if (patchErrors.Count > 0)
            return Results.ValidationProblem(patchErrors);

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.ObjectiveUpdated, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objectiveId,
            before: before,
            after: new { objective.Name, objective.Score, objective.Category, objective.Metadata, objective.Rule, objective.FailRule });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.InvalidTextRepresentation })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Metadata"] = ["One or more of Metadata, Rule, or FailRule is not valid JSON."],
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                detail: "This record was modified by someone else. Reload and reapply your change.",
                statusCode: StatusCodes.Status409Conflict);
        }
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectiveUpdated(objectiveId, eventGameId, eventId, callerId);

        return Results.Ok(new ObjectiveResponse(
            objective.Id, objective.Name, objective.Score, objective.Category, objective.Metadata, objective.IsPredefined, objective.Rule, objective.FailRule));
    }

    internal static async Task<IResult> ImportAllPredefined(
        Guid eventId,
        Guid eventGameId,
        ImportPredefinedObjectivesRequest? request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<ObjectivesEndpoint> logger,
        CancellationToken ct)
    {
        var eventGame = await db.EventGames
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);

        if (eventGame is null)
            return Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(eventGame.Event, principal, "import predefined objectives") is { } ownerError)
            return ownerError;

        var knownGameId = eventGame.KnownGameId;
        if (knownGameId is null)
            return Results.Problem(detail: "Cannot import predefined objectives for a custom game.", statusCode: StatusCodes.Status404NotFound);

        var query = db.Objectives
            .Where(o => o.IsPredefined && o.EventGameId == null && o.GameId == knownGameId);

        var filterIds = request?.ObjectiveIds;
        if (filterIds is { Count: > 0 })
        {
            var idSet = filterIds.ToHashSet();
            query = query.Where(o => idSet.Contains(o.Id));
        }

        // Ordered so the SortOrder handed out below is deterministic: the
        // catalog lists by name, and so does the imported result.
        var predefined = await query.OrderBy(o => o.Name).ThenBy(o => o.Id).ToListAsync(ct);

        if (predefined.Count == 0)
            return Results.Problem(detail: "No predefined objectives are available for this game.", statusCode: StatusCodes.Status404NotFound);

        // Idempotent re-import, keyed the way PredefinedObjectiveSeeder keys
        // the catalog: on the rule, not the display name. The same boss can
        // appear at several locations under one name ("Black Knife Assassin -
        // Slain" in Bellum Highway and in Stormhill) tracking different flags;
        // keying on the name silently dropped every location after the first.
        // A catalog row without a rule has nothing else to identify it by, so
        // the name stands in for that case only.
        var existingKeys = (await db.Objectives
                .Where(o => o.EventGameId == eventGame.Id)
                .Select(o => new { o.Name, o.Rule })
                .ToListAsync(ct))
            .Select(o => ImportIdentity(o.Name, o.Rule))
            .ToHashSet(StringComparer.Ordinal);

        var nextSortOrder = await NextSortOrderAsync(db, eventGame.Id, ct);
        var imported = 0;
        var skipped = 0;
        foreach (var p in predefined)
        {
            if (!existingKeys.Add(ImportIdentity(p.Name, p.Rule)))
            {
                skipped++;
                continue;
            }

            db.Objectives.Add(new Objective
            {
                EventGameId = eventGame.Id,
                Name = p.Name,
                Score = p.Score,
                Category = p.Category,
                Metadata = p.Metadata,
                Rule = p.Rule,
                FailRule = p.FailRule,
                GameId = p.GameId,
                IsPredefined = true,
                SortOrder = nextSortOrder++,
            });
            imported++;
        }

        var callerId = EventOwnership.GetUserId(principal);
        if (imported > 0)
        {
            audit.Log(db, AuditEventTypes.ObjectivePredefinedImported, callerId,
                eventId: eventId, eventGameId: eventGameId,
                after: new { ImportedCount = imported, SkippedCount = skipped });
            await db.SaveChangesAsync(ct);
            await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        }
        logger.PredefinedObjectivesImported(imported, skipped, eventGameId, eventId, callerId);

        return Results.Ok(new ImportPredefinedObjectivesResult(imported, skipped));
    }

    internal static async Task<IResult> DeleteObjective(
        Guid eventId,
        Guid eventGameId,
        Guid objectiveId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<ObjectivesEndpoint> logger,
        CancellationToken ct)
    {
        var eventGame = await db.EventGames
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);

        if (eventGame is null)
            return Results.Problem(detail: "Game is not part of this event.", statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(eventGame.Event, principal, "delete objectives") is { } ownerError)
            return ownerError;

        var objective = await db.Objectives
            .FirstOrDefaultAsync(o => o.Id == objectiveId && o.EventGameId == eventGame.Id, ct);

        if (objective is null)
            return Results.Problem(detail: "Objective not found.", statusCode: StatusCodes.Status404NotFound);

        var completedCount = await db.CompletedObjectives
            .CountAsync(c => c.ObjectiveId == objectiveId, ct);
        var failedCount = await db.FailedObjectives
            .CountAsync(f => f.ObjectiveId == objectiveId, ct);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.ObjectiveDeleted, callerId,
            eventId: eventId, eventGameId: eventGameId, objectiveId: objectiveId,
            before: new
            {
                objective.Name,
                objective.Score,
                objective.Metadata,
                objective.Rule,
                objective.FailRule,
                objective.IsPredefined,
                CompletedCount = completedCount,
                FailedCount = failedCount,
            });

        db.Objectives.Remove(objective);
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.ObjectiveDeleted(objectiveId, eventGameId, eventId, callerId);

        return Results.NoContent();
    }

    private static Dictionary<string, string[]> ValidateObjectiveRequest(
        string? name, int score, string? metadata = null, string? rule = null, string? failRule = null)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["Name"] = ["Name is required."];
        else if (name.Length > MaxNameLength)
            errors["Name"] = [$"Name must be {MaxNameLength} characters or fewer."];

        if (score < 0)
            errors["Score"] = ["Score must be non-negative."];

        if (ObjectiveRuleValidator.ValidateMetadata(metadata) is { } metadataError)
            errors["Metadata"] = [metadataError];
        if (ObjectiveRuleValidator.ValidateRule(rule) is { } ruleError)
            errors["Rule"] = [ruleError];
        if (ObjectiveRuleValidator.ValidateRule(failRule) is { } failRuleError)
            errors["FailRule"] = [failRuleError];

        return errors;
    }
}
