using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Common.Models;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record EventResponse(
    Guid Id,
    string Name,
    string? UrlAlias,
    string Description,
    Guid CreatedById,
    bool IsArchived,
    bool IsStarted,
    bool IsFeatured,
    string TieBreakMode,
    bool AllowTrialRuns,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<EventCompetitorResponse> Competitors,
    List<EventGameResponse> Games);

/// <summary>
/// The events list view's row shape: only what a list card renders, with no
/// competitor/game/objective graph. <see cref="GetEvent"/> still returns the
/// full <see cref="EventResponse"/> for the detail page.
/// </summary>
public sealed record EventListItemResponse(
    Guid Id,
    string Name,
    string? UrlAlias,
    string Description,
    Guid CreatedById,
    bool IsArchived,
    bool IsStarted,
    bool IsFeatured,
    string TieBreakMode,
    bool AllowTrialRuns,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int CompetitorCount,
    int GameCount);

public sealed record EventCompetitorResponse(
    Guid UserId,
    string DisplayName,
    DateTime JoinedAt,
    bool IsStreamer,
    bool IsLive,
    List<EventModeratorResponse> Moderators);

public sealed record EventGameResponse(
    Guid EventGameId,
    int? KnownGameId,
    string GameName,
    string? KnownGameName,
    bool ConnectorSupported,
    string? RequiredConnectorVersion,
    bool IsCustomGame,
    bool IsEnabled,
    string? CustomGameDescription,
    List<ObjectiveResponse> Objectives);

public sealed record ObjectiveResponse(
    Guid Id,
    string Name,
    int Score,
    string? Category,
    string? Metadata,
    bool IsPredefined,
    string? Rule = null,
    string? FailRule = null);

public sealed record CreateEventRequest(string Name, string Description);
public sealed record PatchEventRequest(
    string? Name,
    string? Description,
    string? TieBreakMode,
    string? UrlAlias,
    bool? AllowTrialRuns);

/// <summary>
/// Events themselves: listing, reading, creating, editing, the archive and
/// featured flags, and the start/stop lifecycle.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="CompletedObjectivesEndpoint"/> for why.
/// </summary>
public partial class EventsEndpoint : IEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MinUrlAliasLength = 3;
    private const int MaxUrlAliasLength = 64;
    private const string UrlAliasIndexName = "IX_Events_UrlAlias";
    private const string FeaturedIndexName = "IX_Events_FeaturedEvent";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const string StatusAll = "all";
    private const string StatusLive = "live";
    private const string StatusStopped = "stopped";
    private const string StatusArchived = "archived";
    private const string StatusFeatured = "featured";

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/events");

        group.MapGet("/", ListEvents)
            .WithName("ListEvents")
            .WithSummary("Lists events with pagination")
            .Produces<PaginatedResponse<EventListItemResponse>>(StatusCodes.Status200OK)
            .AllowAnonymous();

        group.MapGet("/featured", GetFeaturedEvent)
            .WithName("GetFeaturedEvent")
            .WithSummary("Gets the currently featured event, if any")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapGet("/{identifier}", GetEvent)
            .WithName("GetEvent")
            .WithSummary("Gets an event by ID or URL alias")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/", CreateEvent)
            .WithName("CreateEvent")
            .WithSummary("Creates a new event (admin only)")
            .Produces<EventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .RequireAdmin();

        group.MapPatch("/{id:guid}", PatchEvent)
            .WithName("PatchEvent")
            .WithSummary("Updates an event (owner or admin)")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPost("/{id:guid}/archive", ArchiveEvent)
            .WithName("ArchiveEvent")
            .WithSummary("Archives an event (owner only, soft delete)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPost("/{id:guid}/unarchive", UnarchiveEvent)
            .WithName("UnarchiveEvent")
            .WithSummary("Restores a previously archived event (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPost("/{id:guid}/start", StartEvent)
            .WithName("StartEvent")
            .WithSummary("Starts an event, allowing objective completions (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPost("/{id:guid}/stop", StopEvent)
            .WithName("StopEvent")
            .WithSummary("Stops an event, preventing objective completions (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPost("/{id:guid}/feature", FeatureEvent)
            .WithName("FeatureEvent")
            .WithSummary("Marks an event as featured, unfeaturing any previously-featured event (admin only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAdmin();

        group.MapPost("/{id:guid}/unfeature", UnfeatureEvent)
            .WithName("UnfeatureEvent")
            .WithSummary("Clears the featured flag on an event (admin only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAdmin();
    }

    private const string LikeEscapeCharacter = "\\";

    /// <summary>
    /// Escapes the three characters LIKE/ILIKE give meaning to, using the
    /// backslash also passed as the pattern's escape character.
    /// </summary>
    internal static string EscapeLikePattern(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    internal static async Task<IResult> ListEvents(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct,
        int page = 1,
        int pageSize = DefaultPageSize,
        bool includeArchived = false,
        string? search = null,
        string? status = StatusAll)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var statusFilter = (status ?? StatusAll).Trim().ToLowerInvariant();

        // Archived events are visible only to their members. An anonymous
        // caller's includeArchived/status=archived falls through to the plain
        // query, which still hides archived rows via the IsArchived soft-delete
        // filter. An authenticated non-admin bypasses that filter only for
        // events they own, compete in, or moderate; admins unrestricted.
        var shouldIncludeArchived = includeArchived || statusFilter == StatusArchived;
        var isAuthenticated = principal.Identity?.IsAuthenticated == true;
        IQueryable<Event> baseQuery;
        if (!shouldIncludeArchived || !isAuthenticated)
        {
            baseQuery = db.Events.AsQueryable();
        }
        else if (EventOwnership.IsAdmin(principal))
        {
            baseQuery = db.Events.IgnoreQueryFilters();
        }
        else
        {
            var callerId = EventOwnership.GetUserId(principal);
            baseQuery = db.Events.IgnoreQueryFilters().Where(e =>
                !e.IsArchived
                || e.CreatedById == callerId
                || e.Competitors.Any(c => c.UserId == callerId)
                || e.Competitors.Any(c => c.Moderators.Any(m => m.ModeratorUserId == callerId)));
        }

        // A list card needs names, flags and counts, so project into that shape
        // rather than Include-ing the full graph: one query with two scalar
        // sub-selects instead of five round trips per page.
        var query = baseQuery.AsNoTracking();

        query = statusFilter switch
        {
            StatusLive => query.Where(e => e.IsStarted),
            StatusStopped => query.Where(e => !e.IsStarted && !e.IsArchived),
            StatusArchived => query.Where(e => e.IsArchived),
            StatusFeatured => query.Where(e => e.IsFeatured),
            _ => query,
        };

        var searchText = (search ?? string.Empty).Trim();
        if (searchText.Length > 0)
        {
            // ILike + the trigram indexes (IX_Events_Name_Trgm,
            // IX_Events_Description_Trgm) gives a bitmap index scan, where
            // LOWER(col).Contains(...) forces a sequential scan on Postgres.
            // The user's text is a literal, not a pattern: "%" and "_" in it
            // would otherwise act as wildcards ("_" alone matched every event).
            var pattern = "%" + EscapeLikePattern(searchText) + "%";
            query = query.Where(e =>
                EF.Functions.ILike(e.Name, pattern, LikeEscapeCharacter) ||
                EF.Functions.ILike(e.Description, pattern, LikeEscapeCharacter));
        }

        query = query.OrderByDescending(e => e.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventListItemResponse(
                e.Id,
                e.Name,
                e.UrlAlias,
                e.Description,
                e.CreatedById,
                e.IsArchived,
                e.IsStarted,
                e.IsFeatured,
                e.TieBreakMode.ToString(),
                e.AllowTrialRuns,
                e.CreatedAt,
                e.UpdatedAt,
                e.Competitors.Count,
                e.EventGames.Count))
            .ToListAsync(ct);

        var response = new PaginatedResponse<EventListItemResponse>(items, totalCount, page, pageSize);

        return Results.Ok(response);
    }

    internal static async Task<IResult> GetEvent(
        string identifier, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var isId = Guid.TryParse(identifier, out var id);
        var normalizedAlias = identifier.ToLowerInvariant();
        // Bypass the global IsArchived query filter so an archived event can
        // still be told apart from a missing one below: a member needs the row,
        // and a non-member needs the exact 404 a missing id would produce, not
        // a 403 that confirms it exists.
        var ev = await db.Events
            .IgnoreQueryFilters()
            .Include(e => e.Competitors).ThenInclude(c => c.User)
            .Include(e => e.Competitors).ThenInclude(c => c.Moderators).ThenInclude(m => m.Moderator)
            .Include(e => e.EventGames).ThenInclude(eg => eg.KnownGame)
            .Include(e => e.EventGames).ThenInclude(eg => eg.Objectives)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => isId ? e.Id == id : e.UrlAlias == normalizedAlias, ct);

        if (ev is null)
            return Results.Problem(
                detail: "Event not found.",
                statusCode: StatusCodes.Status404NotFound);

        // Archived events are visible only to their members: owner, competitors,
        // delegated moderators, admins. Everyone else gets the same 404 as a
        // nonexistent id.
        if (ev.IsArchived && !await EventOwnership.IsEventMemberAsync(ev, principal, db, ct))
            return Results.Problem(
                detail: "Event not found.",
                statusCode: StatusCodes.Status404NotFound);

        return Results.Ok(MapToResponse(ev));
    }

    internal static async Task<IResult> CreateEvent(
        CreateEventRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventsEndpoint> logger,
        CancellationToken ct)
    {
        // Admin-only: regular users can compete, opt into streamer delegation,
        // or be delegated as moderators, but not create events.
        if (!EventOwnership.IsAdmin(principal))
            return Results.Problem(
                detail: "Only admins can create events.",
                statusCode: StatusCodes.Status403Forbidden);

        var validationErrors = ValidateCreateRequest(request);
        if (validationErrors.Count > 0)
            return Results.ValidationProblem(validationErrors);

        var userId = EventOwnership.GetUserId(principal);

        var ev = new Event
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            CreatedById = userId
        };

        db.Events.Add(ev);
        audit.Log(db, AuditEventTypes.EventCreated, userId, eventId: ev.Id,
            after: new { ev.Id, ev.Name, ev.Description, TieBreakMode = ev.TieBreakMode.ToString() });
        await db.SaveChangesAsync(ct);
        logger.EventCreated(ev.Id, userId);

        return Results.Created($"/api/v1/events/{ev.Id}", MapToResponse(ev));
    }

    internal static async Task<IResult> PatchEvent(
        Guid id,
        PatchEventRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventsEndpoint> logger,
        CancellationToken ct)
    {
        var ev = await db.Events
            .Include(e => e.Competitors).ThenInclude(c => c.User)
            .Include(e => e.Competitors).ThenInclude(c => c.Moderators).ThenInclude(m => m.Moderator)
            .Include(e => e.EventGames).ThenInclude(eg => eg.KnownGame)
            .Include(e => e.EventGames).ThenInclude(eg => eg.Objectives)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (ev is null)
            return Results.Problem(
                detail: "Event not found.",
                statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(ev, principal, "update this event") is { } ownerError)
            return ownerError;

        // Snapshot the writable fields so the audit reflects exactly the
        // before/after delta the API actually surfaces on PATCH.
        var before = new { ev.Name, ev.UrlAlias, ev.Description, TieBreakMode = ev.TieBreakMode.ToString(), ev.AllowTrialRuns };

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > MaxNameLength)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = [$"Name is required and must be {MaxNameLength} characters or fewer."]
                });
            ev.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            if (request.Description.Length > MaxDescriptionLength)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Description"] = [$"Description must be {MaxDescriptionLength} characters or fewer."]
                });
            ev.Description = request.Description.Trim();
        }

        if (request.UrlAlias is not null)
        {
            var requestedAlias = request.UrlAlias.Trim();
            if (requestedAlias.Length == 0)
            {
                ev.UrlAlias = null;
            }
            else
            {
                if (!IsValidUrlAlias(requestedAlias))
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["UrlAlias"] =
                        [
                            $"URL alias must be {MinUrlAliasLength}-{MaxUrlAliasLength} lowercase letters or numbers separated by single hyphens, and cannot be a UUID."
                        ]
                    });

                if (await db.Events.IgnoreQueryFilters()
                    .AnyAsync(e => e.Id != id && e.UrlAlias == requestedAlias, ct))
                    return Results.Problem(
                        detail: "URL alias is already in use.",
                        statusCode: StatusCodes.Status409Conflict);

                ev.UrlAlias = requestedAlias;
            }
        }

        if (request.TieBreakMode is not null)
        {
            if (!Enum.TryParse<TieBreakMode>(request.TieBreakMode, ignoreCase: false, out var mode))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["TieBreakMode"] = [$"TieBreakMode must be one of: {string.Join(", ", Enum.GetNames<TieBreakMode>())}."]
                });
            ev.TieBreakMode = mode;
        }

        // Deliberately does not touch existing runs: revoking the switch only
        // stops NEW trials (Enable and Start both check it). A run already
        // Running keeps recording and publishing until the competitor stops or
        // disables it — this is not a kill switch for runs in flight.
        if (request.AllowTrialRuns is { } allowTrialRuns)
            ev.AllowTrialRuns = allowTrialRuns;

        ev.UpdatedAt = DateTime.UtcNow;
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventUpdated, callerId, eventId: ev.Id,
            before: before,
            after: new { ev.Name, ev.UrlAlias, ev.Description, TieBreakMode = ev.TieBreakMode.ToString(), ev.AllowTrialRuns });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: UrlAliasIndexName
            })
        {
            return Results.Problem(
                detail: "URL alias is already in use.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                detail: "This record was modified by someone else. Reload and reapply your change.",
                statusCode: StatusCodes.Status409Conflict);
        }
        logger.EventUpdated(ev.Id, callerId);

        return Results.Ok(MapToResponse(ev));
    }

    internal static async Task<IResult> ArchiveEvent(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventsEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireOwnedEventAsync(id, principal, "archive this event", db, ct);
        if (error is not null) return error;

        ev!.IsArchived = true;
        ev.UpdatedAt = DateTime.UtcNow;
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventArchived, callerId, eventId: ev.Id,
            before: new { IsArchived = false }, after: new { IsArchived = true });
        await db.SaveChangesAsync(ct);
        logger.EventArchived(ev.Id, callerId);

        return Results.NoContent();
    }

    internal static async Task<IResult> UnarchiveEvent(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventsEndpoint> logger,
        CancellationToken ct)
    {
        // Bypass the global IsArchived query filter; the whole point is to
        // operate on a row that's currently hidden from regular queries.
        var ev = await db.Events.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (ev is null)
            return Results.Problem(
                detail: "Event not found.",
                statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(ev, principal, "unarchive this event") is { } ownerError)
            return ownerError;

        ev.IsArchived = false;
        ev.UpdatedAt = DateTime.UtcNow;
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventUnarchived, callerId, eventId: ev.Id,
            before: new { IsArchived = true }, after: new { IsArchived = false });
        await db.SaveChangesAsync(ct);
        logger.EventUnarchived(ev.Id, callerId);

        return Results.NoContent();
    }

    internal static async Task<IResult> StartEvent(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventsEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireOwnedEventAsync(id, principal, "start this event", db, ct);
        if (error is not null) return error;

        var nowUtc = DateTime.UtcNow;
        ev!.IsStarted = true;
        ev.StartedAt = nowUtc;
        ev.StoppedAt = null;
        ev.UpdatedAt = nowUtc;
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventStarted, callerId, eventId: ev.Id,
            before: new { IsStarted = false }, after: new { IsStarted = true });
        await db.SaveChangesAsync(ct);
        logger.EventStarted(ev.Id, callerId);

        return Results.NoContent();
    }

    internal static async Task<IResult> StopEvent(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventsEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireOwnedEventAsync(id, principal, "stop this event", db, ct);
        if (error is not null) return error;

        var hasEnabledGame = await db.EventGames.AnyAsync(eg => eg.EventId == id && eg.IsEnabled, ct);
        if (hasEnabledGame)
            return Results.Problem(
                detail: "Cannot stop the event while a game is still active. Disable all games first.",
                statusCode: StatusCodes.Status409Conflict);

        var nowUtc = DateTime.UtcNow;
        ev!.IsStarted = false;
        ev.StoppedAt = nowUtc;
        ev.UpdatedAt = nowUtc;
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventStopped, callerId, eventId: ev.Id,
            before: new { IsStarted = true }, after: new { IsStarted = false });
        await db.SaveChangesAsync(ct);
        logger.EventStopped(ev.Id, callerId);

        return Results.NoContent();
    }

    internal static async Task<IResult> GetFeaturedEvent(AppDbContext db, CancellationToken ct)
    {
        var ev = await db.Events
            .Include(e => e.Competitors).ThenInclude(c => c.User)
            .Include(e => e.Competitors).ThenInclude(c => c.Moderators).ThenInclude(m => m.Moderator)
            .Include(e => e.EventGames).ThenInclude(eg => eg.KnownGame)
            .Include(e => e.EventGames).ThenInclude(eg => eg.Objectives)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.IsFeatured, ct);

        if (ev is null)
            return Results.Problem(
                detail: "No event is currently featured.",
                statusCode: StatusCodes.Status404NotFound);

        return Results.Ok(MapToResponse(ev));
    }

    internal static async Task<IResult> FeatureEvent(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventsEndpoint> logger,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal))
            return Results.Problem(
                detail: "Only admins can feature an event.",
                statusCode: StatusCodes.Status403Forbidden);

        var (ev, error) = await EventContext.RequireEventAsync(id, db, ct);
        if (error is not null) return error;

        // The partial unique index IX_Events_FeaturedEvent permits at most one
        // IsFeatured row site-wide, so any other event must be unfeatured before
        // this one is set, inside one transaction — read-then-write in
        // application code cannot hold under concurrent feature calls.
        var nowUtc = DateTime.UtcNow;
        var callerId = EventOwnership.GetUserId(principal);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        await db.Events
            .Where(e => e.IsFeatured && e.Id != id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.IsFeatured, false)
                .SetProperty(e => e.UpdatedAt, nowUtc), ct);

        ev!.IsFeatured = true;
        ev.UpdatedAt = nowUtc;
        audit.Log(db, AuditEventTypes.EventFeatured, callerId, eventId: ev.Id,
            before: new { IsFeatured = false }, after: new { IsFeatured = true });

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: FeaturedIndexName
            })
        {
            return Results.Problem(
                detail: "Another event was featured concurrently. Retry.",
                statusCode: StatusCodes.Status409Conflict);
        }

        logger.EventFeatured(ev.Id, callerId);

        return Results.NoContent();
    }

    internal static async Task<IResult> UnfeatureEvent(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<EventsEndpoint> logger,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal))
            return Results.Problem(
                detail: "Only admins can unfeature an event.",
                statusCode: StatusCodes.Status403Forbidden);

        var (ev, error) = await EventContext.RequireEventAsync(id, db, ct);
        if (error is not null) return error;

        ev!.IsFeatured = false;
        ev.UpdatedAt = DateTime.UtcNow;
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventUnfeatured, callerId, eventId: ev.Id,
            before: new { IsFeatured = true }, after: new { IsFeatured = false });
        await db.SaveChangesAsync(ct);
        logger.EventUnfeatured(ev.Id, callerId);

        return Results.NoContent();
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateEventRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors["Name"] = ["Name is required."];
        else if (request.Name.Length > MaxNameLength)
            errors["Name"] = [$"Name must be {MaxNameLength} characters or fewer."];

        if (request.Description?.Length > MaxDescriptionLength)
            errors["Description"] = [$"Description must be {MaxDescriptionLength} characters or fewer."];

        return errors;
    }

    private static bool IsValidUrlAlias(string value) =>
        value.Length is >= MinUrlAliasLength and <= MaxUrlAliasLength
        && !Guid.TryParse(value, out _)
        && UrlAliasRegex().IsMatch(value);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex UrlAliasRegex();

    internal static EventResponse MapToResponse(Event ev) => new(
        ev.Id,
        ev.Name,
        ev.UrlAlias,
        ev.Description,
        ev.CreatedById,
        ev.IsArchived,
        ev.IsStarted,
        ev.IsFeatured,
        ev.TieBreakMode.ToString(),
        ev.AllowTrialRuns,
        ev.CreatedAt,
        ev.UpdatedAt,
        ev.Competitors.Select(c => new EventCompetitorResponse(
            c.UserId,
            c.User.DisplayName,
            c.JoinedAt,
            c.IsStreamer,
            c.IsLive,
            c.Moderators
                .OrderBy(m => m.AddedAt)
                .Select(m => new EventModeratorResponse(m.ModeratorUserId, m.Moderator.DisplayName, m.AddedAt))
                .ToList())).ToList(),
        ev.EventGames.OrderBy(eg => eg.SortOrder).Select(eg => new EventGameResponse(
            eg.Id,
            eg.KnownGameId,
            eg.CustomGameName ?? eg.KnownGame?.Name ?? "Unknown",
            eg.KnownGame?.Name,
            !eg.IsCustomGame && eg.KnownGame!.ConnectorSupported,
            eg.IsCustomGame ? null : eg.KnownGame!.RequiredConnectorVersion,
            eg.IsCustomGame,
            eg.IsEnabled,
            eg.CustomGameDescription,
            eg.Objectives.OrderBy(o => o.SortOrder).Select(o => new ObjectiveResponse(
                o.Id, o.Name, o.Score, o.Category, o.Metadata, o.IsPredefined, o.Rule, o.FailRule)).ToList())).ToList());
}
