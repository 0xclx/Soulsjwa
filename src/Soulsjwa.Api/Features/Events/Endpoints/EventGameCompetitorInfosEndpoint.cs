using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record CompetitorInfoResponse(
    Guid Id,
    Guid EventGameId,
    Guid UserId,
    string Type,
    string? Url,
    string? Text,
    Guid CreatedById,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCompetitorInfoRequest(string Type, string? Url, string? Text);
public sealed record PatchCompetitorInfoRequest(string? Type, string? Url, string? Text);

/// <summary>
/// CRUD for additional metadata attached to a (EventGame, Competitor) pair —
/// e.g. a death-clip Twitch/YouTube URL, an arbitrary link, or a plain text
/// note. See <see cref="EventGameCompetitorInfo"/> for the data model and
/// <see cref="EventOwnership.RequireCanEditCompetitorInfoAsync"/> for the
/// authoring rules.
/// </summary>
public class EventGameCompetitorInfosEndpoint : IEndpoint
{
    public const int MaxUrlLength = 2048;
    public const int MaxTextLength = 2000;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(
            ApiRoutes.Prefix + "/events/{eventId:guid}/games/{eventGameId:guid}/competitors/{userId:guid}/infos");

        group.MapGet("/", List)
            .WithName("ListCompetitorInfos")
            .WithSummary("Lists metadata entries for a competitor on a specific event game")
            .Produces<List<CompetitorInfoResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .CacheOutput("Scoreboard")
            .AllowAnonymous();

        group.MapPost("/", Create)
            .WithName("CreateCompetitorInfo")
            .WithSummary("Adds a metadata entry for a competitor on an event game")
            .Produces<CompetitorInfoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPatch("/{infoId:guid}", Patch)
            .WithName("PatchCompetitorInfo")
            .WithSummary("Updates a metadata entry (last-write-wins)")
            .Produces<CompetitorInfoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapDelete("/{infoId:guid}", Remove)
            .WithName("RemoveCompetitorInfo")
            .WithSummary("Hard-deletes a metadata entry")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    private static async Task<IResult> List(
        Guid eventId, Guid eventGameId, Guid userId, AppDbContext db, CancellationToken ct)
    {
        // Validate the (event, game, competitor) triple exists. Returning 404
        // for any missing piece keeps the API surface from leaking whether the
        // game or competitor exists separately.
        var contextExists = await db.EventGames
            .AnyAsync(eg => eg.Id == eventGameId && eg.EventId == eventId, ct);
        if (!contextExists)
            return Results.Problem(detail: "Event game not found.", statusCode: StatusCodes.Status404NotFound);

        var infos = await db.EventGameCompetitorInfos
            .Where(i => i.EventGameId == eventGameId && i.UserId == userId)
            .OrderBy(i => i.CreatedAt)
            .Select(i => ToResponse(i))
            .ToListAsync(ct);

        return Results.Ok(infos);
    }

    private static async Task<IResult> Create(
        Guid eventId,
        Guid eventGameId,
        Guid userId,
        CreateCompetitorInfoRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGameCompetitorInfosEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var eventGame = await db.EventGames
            .FirstOrDefaultAsync(eg => eg.Id == eventGameId && eg.EventId == eventId, ct);
        if (eventGame is null)
            return Results.Problem(detail: "Event game not found.", statusCode: StatusCodes.Status404NotFound);

        var isCompetitor = await db.EventCompetitors
            .AnyAsync(ec => ec.EventId == eventId && ec.UserId == userId, ct);
        if (!isCompetitor)
            return Results.Problem(detail: "User is not a competitor in this event.", statusCode: StatusCodes.Status404NotFound);

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        if (!TryParseType(request.Type, out var type, out var typeError))
            return Results.ValidationProblem(typeError);

        if (ValidatePayload(type, request.Url, request.Text) is { } validationError)
            return Results.ValidationProblem(validationError);

        var callerId = EventOwnership.GetUserId(principal);
        var now = DateTime.UtcNow;
        var info = new EventGameCompetitorInfo
        {
            EventGameId = eventGameId,
            UserId = userId,
            Type = type,
            Url = NormalizeUrl(request.Url),
            Text = NormalizeText(request.Text),
            CreatedById = callerId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.EventGameCompetitorInfos.Add(info);
        audit.Log(db, AuditEventTypes.CompetitorInfoAdded, callerId,
            eventId: eventId, eventGameId: eventGameId, subjectUserId: userId,
            after: new { info.Id, Type = info.Type.ToString(), info.Url, info.Text });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.CompetitorInfoAdded(info.Id, userId, eventGameId, eventId, callerId);

        return Results.Created(
            $"/api/v1/events/{eventId}/games/{eventGameId}/competitors/{userId}/infos/{info.Id}",
            ToResponse(info));
    }

    private static async Task<IResult> Patch(
        Guid eventId,
        Guid eventGameId,
        Guid userId,
        Guid infoId,
        PatchCompetitorInfoRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGameCompetitorInfosEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var info = await db.EventGameCompetitorInfos
            .Include(i => i.EventGame)
            .FirstOrDefaultAsync(i => i.Id == infoId
                                   && i.EventGameId == eventGameId
                                   && i.UserId == userId
                                   && i.EventGame.EventId == eventId, ct);
        if (info is null)
            return Results.Problem(detail: "Info not found.", statusCode: StatusCodes.Status404NotFound);

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        var newType = info.Type;
        if (request.Type is not null)
        {
            if (!TryParseType(request.Type, out newType, out var typeError))
                return Results.ValidationProblem(typeError);
        }

        // PATCH semantics: missing fields fall back to the current values.
        var newUrl = request.Url is not null ? NormalizeUrl(request.Url) : info.Url;
        var newText = request.Text is not null ? NormalizeText(request.Text) : info.Text;

        if (ValidatePayload(newType, newUrl, newText) is { } validationError)
            return Results.ValidationProblem(validationError);

        var before = new { Type = info.Type.ToString(), info.Url, info.Text };
        info.Type = newType;
        info.Url = newUrl;
        info.Text = newText;
        info.UpdatedAt = DateTime.UtcNow;
        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.CompetitorInfoUpdated, actorId,
            eventId: eventId, eventGameId: eventGameId, subjectUserId: userId,
            before: before,
            after: new { Type = info.Type.ToString(), info.Url, info.Text });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.CompetitorInfoUpdated(info.Id, userId, eventGameId, eventId, actorId);

        return Results.Ok(ToResponse(info));
    }

    private static async Task<IResult> Remove(
        Guid eventId,
        Guid eventGameId,
        Guid userId,
        Guid infoId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<EventGameCompetitorInfosEndpoint> logger,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var info = await db.EventGameCompetitorInfos
            .Include(i => i.EventGame)
            .FirstOrDefaultAsync(i => i.Id == infoId
                                   && i.EventGameId == eventGameId
                                   && i.UserId == userId
                                   && i.EventGame.EventId == eventId, ct);
        if (info is null)
            return Results.Problem(detail: "Info not found.", statusCode: StatusCodes.Status404NotFound);

        if (await EventOwnership.RequireCanEditCompetitorInfoAsync(ev!, principal, userId, db, ct) is { } authError)
            return authError;

        db.EventGameCompetitorInfos.Remove(info);
        var removedActorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.CompetitorInfoRemoved, removedActorId,
            eventId: eventId, eventGameId: eventGameId, subjectUserId: userId,
            before: new { info.Id, Type = info.Type.ToString(), info.Url, info.Text });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        logger.CompetitorInfoRemoved(info.Id, userId, eventGameId, eventId, removedActorId);

        return Results.NoContent();
    }

    private static CompetitorInfoResponse ToResponse(EventGameCompetitorInfo i) => new(
        i.Id, i.EventGameId, i.UserId, i.Type.ToString(), i.Url, i.Text,
        i.CreatedById, i.CreatedAt, i.UpdatedAt);

    private static bool TryParseType(string? raw, out EventGameCompetitorInfoType type, out Dictionary<string, string[]> error)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && Enum.TryParse<EventGameCompetitorInfoType>(raw, ignoreCase: false, out type)
            && Enum.IsDefined(type))
        {
            error = null!;
            return true;
        }
        type = default;
        var allowed = string.Join(", ", Enum.GetNames<EventGameCompetitorInfoType>());
        error = new Dictionary<string, string[]>
        {
            ["Type"] = [$"Type must be one of: {allowed}."]
        };
        return false;
    }

    private static Dictionary<string, string[]>? ValidatePayload(
        EventGameCompetitorInfoType type, string? url, string? text)
    {
        switch (type)
        {
            case EventGameCompetitorInfoType.DeathClip:
                if (string.IsNullOrWhiteSpace(url))
                    return new() { ["Url"] = ["Url is required for DeathClip."] };
                if (url.Length > MaxUrlLength)
                    return new() { ["Url"] = [$"Url must be {MaxUrlLength} characters or fewer."] };
                if (!CompetitorInfoUrlValidator.IsValidDeathClipUrl(url))
                    return new() { ["Url"] = ["DeathClip Url must be an https Twitch or YouTube URL."] };
                if (text is not null && text.Length > MaxTextLength)
                    return new() { ["Text"] = [$"Text must be {MaxTextLength} characters or fewer."] };
                return null;

            case EventGameCompetitorInfoType.Link:
                if (string.IsNullOrWhiteSpace(url))
                    return new() { ["Url"] = ["Url is required for Link."] };
                if (url.Length > MaxUrlLength)
                    return new() { ["Url"] = [$"Url must be {MaxUrlLength} characters or fewer."] };
                if (!CompetitorInfoUrlValidator.IsValidLinkUrl(url))
                    return new() { ["Url"] = ["Link Url must be a valid http(s) URL."] };
                if (text is not null && text.Length > MaxTextLength)
                    return new() { ["Text"] = [$"Text must be {MaxTextLength} characters or fewer."] };
                return null;

            case EventGameCompetitorInfoType.Other:
                if (string.IsNullOrWhiteSpace(text))
                    return new() { ["Text"] = ["Text is required for Other."] };
                if (text.Length > MaxTextLength)
                    return new() { ["Text"] = [$"Text must be {MaxTextLength} characters or fewer."] };
                return null;

            default:
                return new() { ["Type"] = ["Unsupported type."] };
        }
    }

    private static string? NormalizeUrl(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null : url.Trim();

    private static string? NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
