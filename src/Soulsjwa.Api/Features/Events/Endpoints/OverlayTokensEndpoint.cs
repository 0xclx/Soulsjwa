using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Events.Endpoints;

/// <param name="Settings">The saved look of the token's OBS source; null when its URL alone drives it.</param>
public sealed record OverlayTokenResponse(
    Guid Id,
    string Name,
    string TokenPrefix,
    Guid CreatedById,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    OverlayTokenSettings? Settings);

/// <summary>
/// Returned only at creation time; <c>Token</c> is the raw value the caller
/// must copy. It is not stored in cleartext and cannot be retrieved later.
/// </summary>
public sealed record CreateOverlayTokenResponse(
    Guid Id,
    string Name,
    string Token,
    string TokenPrefix,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    OverlayTokenSettings? Settings);

/// <param name="Settings">Optional look to save with the token, so the designed overlay and its URL are minted together.</param>
public sealed record CreateOverlayTokenRequest(string Name, OverlayTokenSettings? Settings = null);

/// <summary>
/// What the OBS browser source polls: the scoreboard plus the token's saved
/// look, together so one request per refresh carries everything the page
/// needs and a look change rides the same cache eviction as a score change.
/// </summary>
public sealed record OverlayScoreboardResponse(ScoreboardResponse Scoreboard, OverlayTokenSettings? Settings);

/// <summary>
/// Handlers are <c>internal static</c> so the integration suite can call them
/// directly with a real database and no HTTP pipeline.
/// </summary>
public class OverlayTokensEndpoint : IEndpoint
{
    private const int MaxNameLength = 100;

    // Throttles writes to `LastUsedAt` so heavy OBS polling doesn't generate
    // a DB write on every request — mirrors `ApiKeyAuthHandler`'s 5-minute
    // policy for the same reason.
    private const int LastUsedAtUpdateIntervalMinutes = 5;

    /// <summary>Newly minted overlay tokens expire after this many days.</summary>
    internal const int DefaultExpiryDays = 90;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/events/{eventId:guid}/overlay-tokens");

        group.MapGet("/", ListTokens)
            .WithName("ListOverlayTokens")
            .WithSummary("Lists overlay tokens for an event. Owners/admins see all tokens for the event; competitors see only the tokens they created.")
            .Produces<List<OverlayTokenResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPost("/", CreateToken)
            .WithName("CreateOverlayToken")
            .WithSummary("Mints a new overlay token for an event. The caller must be the event owner, an admin, or a competitor in the event; competitors mint tokens scoped to themselves.")
            .Produces<CreateOverlayTokenResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapDelete("/{tokenId:guid}", RevokeToken)
            .WithName("RevokeOverlayToken")
            .WithSummary("Revokes an overlay token. Owners/admins may revoke any token for the event; competitors may revoke only their own.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPut("/{tokenId:guid}/settings", UpdateSettings)
            .WithName("UpdateOverlayTokenSettings")
            .WithSummary("Saves how the token's OBS source looks. Any source already polling with the token picks the change up on its next refresh. Owners/admins may edit any token for the event; competitors only their own.")
            .Produces<OverlayTokenResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        // The token-gated scoreboard the OBS browser source polls. It is
        // `AllowAnonymous` only in that it needs no session cookie or
        // `X-Api-Key`: the overlay token IS the credential and is validated
        // below. Output-cached so a streamer's overlay costs the DB no more
        // than the in-app scoreboard already does.
        app.MapGet(ApiRoutes.Prefix + "/events/{eventId:guid}/overlay-scoreboard", GetOverlayScoreboard)
            .WithName("GetOverlayScoreboard")
            .WithSummary("Gets the scoreboard and the token's saved look for the OBS overlay; authenticated by an overlay token in the query string rather than a session or API key.")
            .Produces<OverlayScoreboardResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .CacheOutput("OverlayScoreboard")
            .AllowAnonymous();
    }

    private static async Task<IResult> ListTokens(
        Guid eventId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var callerId = EventOwnership.GetUserId(principal);
        var canSeeAll = EventOwnership.IsAdmin(principal) || ev!.CreatedById == callerId;

        if (!canSeeAll)
        {
            var isCompetitor = await db.EventCompetitors
                .AnyAsync(c => c.EventId == eventId && c.UserId == callerId, ct);
            if (!isCompetitor)
                return Results.Problem(
                    detail: "Only the event owner, an admin, or a competitor in the event can view overlay tokens.",
                    statusCode: StatusCodes.Status403Forbidden);
        }

        var query = db.EventOverlayTokens
            .Where(t => t.EventId == eventId && !t.IsRevoked);
        if (!canSeeAll)
            query = query.Where(t => t.CreatedById == callerId);

        var tokens = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(tokens.Select(ToResponse).ToList());
    }

    internal static async Task<IResult> CreateToken(
        Guid eventId,
        CreateOverlayTokenRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var callerId = EventOwnership.GetUserId(principal);
        var isOwnerOrAdmin = EventOwnership.IsAdmin(principal) || ev!.CreatedById == callerId;
        if (!isOwnerOrAdmin)
        {
            var isCompetitor = await db.EventCompetitors
                .AnyAsync(c => c.EventId == eventId && c.UserId == callerId, ct);
            if (!isCompetitor)
                return Results.Problem(
                    detail: "Only the event owner, an admin, or a competitor in the event can create overlay tokens.",
                    statusCode: StatusCodes.Status403Forbidden);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
            errors[nameof(request.Name)] = [$"Name is required and must be {MaxNameLength} characters or fewer."];

        var settings = request.Settings?.Normalized();
        if (settings is not null)
            foreach (var (field, messages) in await OverlayTokenSettingsValidator.ValidateAsync(settings, eventId, db, ct))
                errors[field] = messages;

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var (raw, prefix) = OverlayToken.Generate();
        var token = new EventOverlayToken
        {
            EventId = eventId,
            Name = name,
            TokenHash = OverlayToken.Hash(raw),
            TokenPrefix = prefix,
            // Token is scoped to whoever minted it: a competitor can only
            // ever revoke or list their own; owners/admins see everything.
            CreatedById = callerId,
            ExpiresAt = DateTime.UtcNow.AddDays(DefaultExpiryDays),
            SettingsJson = settings is null ? null : OverlayTokenSettingsJson.Serialize(settings),
        };
        db.EventOverlayTokens.Add(token);
        audit.Log(db, AuditEventTypes.OverlayTokenCreated, callerId,
            eventId: eventId,
            after: new { token.Id, token.Name, token.TokenPrefix, token.ExpiresAt, Settings = settings });
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/v1/events/{eventId}/overlay-tokens/{token.Id}",
            new CreateOverlayTokenResponse(
                token.Id, token.Name, raw, token.TokenPrefix, token.CreatedAt, token.ExpiresAt, settings));
    }

    internal static async Task<IResult> UpdateSettings(
        Guid eventId,
        Guid tokenId,
        OverlayTokenSettings request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var token = await db.EventOverlayTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.EventId == eventId && !t.IsRevoked, ct);
        if (token is null)
            return Results.Problem(detail: "Overlay token not found.", statusCode: StatusCodes.Status404NotFound);

        // Same rule as revoking: the token's creator, or anyone who may
        // manage every token on the event.
        var callerId = EventOwnership.GetUserId(principal);
        var canEditAny = EventOwnership.IsAdmin(principal) || ev!.CreatedById == callerId;
        if (!canEditAny && token.CreatedById != callerId)
            return Results.Problem(
                detail: "Only the event owner, an admin, or the token's creator can change an overlay token's look.",
                statusCode: StatusCodes.Status403Forbidden);

        var settings = request.Normalized();
        var errors = await OverlayTokenSettingsValidator.ValidateAsync(settings, eventId, db, ct);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var before = OverlayTokenSettingsJson.Deserialize(token.SettingsJson);
        token.SettingsJson = OverlayTokenSettingsJson.Serialize(settings);
        audit.Log(db, AuditEventTypes.OverlayTokenSettingsUpdated, callerId,
            eventId: eventId,
            before: new { token.Id, token.Name, Settings = before },
            after: new { token.Id, token.Name, Settings = settings });
        await db.SaveChangesAsync(ct);

        // The OBS source reads its look from the same cached response as the
        // scoreboard, so this is what makes the change reach it within one
        // refresh rather than after the cache's own expiry.
        await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);

        return Results.Ok(ToResponse(token));
    }

    private static OverlayTokenResponse ToResponse(EventOverlayToken t) => new(
        t.Id, t.Name, t.TokenPrefix, t.CreatedById, t.CreatedAt, t.LastUsedAt, t.ExpiresAt,
        OverlayTokenSettingsJson.Deserialize(t.SettingsJson));

    private static async Task<IResult> RevokeToken(
        Guid eventId,
        Guid tokenId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var token = await db.EventOverlayTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.EventId == eventId, ct);
        if (token is null)
            return Results.Problem(detail: "Overlay token not found.", statusCode: StatusCodes.Status404NotFound);

        var callerId = EventOwnership.GetUserId(principal);
        var canRevokeAny = EventOwnership.IsAdmin(principal) || ev!.CreatedById == callerId;
        if (!canRevokeAny && token.CreatedById != callerId)
            return Results.Problem(
                detail: "Only the event owner, an admin, or the token's creator can revoke an overlay token.",
                statusCode: StatusCodes.Status403Forbidden);

        if (!token.IsRevoked)
        {
            token.IsRevoked = true;
            audit.Log(db, AuditEventTypes.OverlayTokenRevoked, callerId,
                eventId: eventId,
                before: new { token.Id, token.Name, token.TokenPrefix });
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    internal static async Task<IResult> GetOverlayScoreboard(
        Guid eventId,
        AppDbContext db,
        HttpContext context,
        CancellationToken ct,
        string? token = null)
    {
        // A URL carrying a secret must never be re-sent as a Referer to whatever
        // the overlay page links to or embeds. Set before any early return so it
        // applies to the 401 case too.
        context.Response.Headers["Referrer-Policy"] = "no-referrer";

        // The header is the preferred credential (never ends up in a URL); an
        // OBS browser source is a bare URL and can't set headers, so the
        // query parameter stays as a fallback. Header wins when both are sent.
        string? headerToken = context.Request.Headers[OverlayToken.HeaderName];
        var effectiveToken = !string.IsNullOrWhiteSpace(headerToken) ? headerToken : token;

        if (string.IsNullOrWhiteSpace(effectiveToken) || !OverlayToken.TryExtractPrefix(effectiveToken, out var prefix))
            // Bodiless like every other 401 (see docs/agent-conventions/backend-endpoints.md).
            return Results.Unauthorized();

        var hash = OverlayToken.Hash(effectiveToken);
        var nowUtc = DateTime.UtcNow;
        var stored = await db.EventOverlayTokens
            .FirstOrDefaultAsync(
                t => t.EventId == eventId
                  && t.TokenPrefix == prefix
                  && t.TokenHash == hash
                  && !t.IsRevoked
                  && (t.ExpiresAt == null || t.ExpiresAt > nowUtc),
                ct);
        if (stored is null)
            // Bodiless like every other 401 (see docs/agent-conventions/backend-endpoints.md).
            return Results.Unauthorized();

        var response = await ScoreboardEndpoint.BuildAsync(eventId, db, ct);
        if (response is null)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        // Throttle LastUsedAt writes to keep the output cache useful.
        if (!stored.LastUsedAt.HasValue
            || (nowUtc - stored.LastUsedAt.Value).TotalMinutes >= LastUsedAtUpdateIntervalMinutes)
        {
            stored.LastUsedAt = nowUtc;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new OverlayScoreboardResponse(response, OverlayTokenSettingsJson.Deserialize(stored.SettingsJson)));
    }
}
