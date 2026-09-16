using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Services;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.TwitchExtension.Endpoints;

/// <summary>Whether this server backs a Twitch extension at all, and which one — anonymous, so the web app can decide whether to show its card.</summary>
public sealed record TwitchExtensionStatusResponse(bool Configured, string? ClientId);

/// <summary>
/// The extension-wide rules an admin set (<see cref="TwitchExtensionSettings"/>),
/// which every channel's board and config view honour.
/// </summary>
public sealed record TwitchExtensionPolicyResponse(
    bool AllowChannelEventChoice,
    bool AllowViewerScopeSwitch,
    string DefaultScope,
    bool DefaultHighlightChannelCompetitor,
    bool DefaultShowTrialProgress);

/// <summary>
/// A channel's effective presentation: what the broadcaster saved, or the
/// admin's defaults while they saved nothing. <c>EventId</c> null means
/// "follow the featured event", the state every channel starts in, and the
/// only state while the admin disallows picks.
/// </summary>
public sealed record TwitchExtensionSettingsResponse(
    Guid? EventId,
    string DefaultScope,
    Guid? PinnedEventGameId,
    bool HighlightChannelCompetitor,
    bool ShowTrialProgress);

/// <summary>The event a channel resolved to, with <c>Source</c> saying why (<see cref="TwitchExtensionEventSource"/> by name).</summary>
public sealed record TwitchExtensionEventResponse(
    Guid Id,
    string Name,
    string? UrlAlias,
    bool IsStarted,
    bool IsFeatured,
    string TieBreakMode,
    Guid? ActiveEventGameId,
    string Source);

public sealed record TwitchExtensionGameResponse(
    Guid EventGameId,
    string Name,
    bool IsEnabled,
    int SortOrder,
    int TotalObjectives);

/// <summary>
/// One competitor's figures in one game — everything the panel needs to
/// narrow a row to that game, without the objective list
/// (<see cref="TwitchExtensionCompetitorDetailResponse"/> carries that, on demand).
/// </summary>
public sealed record TwitchExtensionEntryGameResponse(
    Guid EventGameId,
    int Score,
    int CompletedCount,
    int FailedCount,
    DateTime? LastCompletedAt,
    bool IsTrialActive,
    TrialProgress? Trial);

public sealed record TwitchExtensionEntryResponse(
    Guid UserId,
    string DisplayName,
    string TwitchLogin,
    string? ProfileImageUrl,
    bool IsLive,
    int Rank,
    int TotalScore,
    int CompletedCount,
    int FailedCount,
    bool IsFinished,
    string Status,
    DateTime? LastCompletedAt,
    long? TotalInGameTimeMs,
    List<TwitchExtensionEntryGameResponse> Games);

/// <summary>
/// The panel's whole poll payload. Deliberately not <see cref="ScoreboardResponse"/>:
/// that embeds every objective for every competitor and game, megabytes for a
/// large Elden Ring event, and every viewer would pull it every few seconds.
/// <c>Event</c> is null when the channel has nothing to show; the panel renders
/// a quiet empty state, never an error.
/// </summary>
public sealed record TwitchExtensionScoreboardResponse(
    string ChannelId,
    TwitchExtensionPolicyResponse Policy,
    TwitchExtensionSettingsResponse Settings,
    TwitchExtensionEventResponse? Event,
    Guid? ChannelCompetitorUserId,
    List<TwitchExtensionGameResponse> Games,
    List<TwitchExtensionEntryResponse> Entries);

public sealed record TwitchExtensionCompetitorGameDetailResponse(
    Guid EventGameId,
    string GameName,
    bool IsEnabled,
    bool IsTrialActive,
    List<ObjectiveDetail> Objectives);

/// <summary>One competitor's objective list, fetched only when a viewer expands their row.</summary>
public sealed record TwitchExtensionCompetitorDetailResponse(
    Guid UserId,
    string DisplayName,
    List<TwitchExtensionCompetitorGameDetailResponse> Games);

public sealed record TwitchExtensionEventOptionGameResponse(Guid EventGameId, string Name, bool IsEnabled);

/// <summary>An event the broadcaster can pick, with whether their linked account competes in it.</summary>
public sealed record TwitchExtensionEventOptionResponse(
    Guid Id,
    string Name,
    bool IsStarted,
    bool IsFeatured,
    bool IsCompetitor,
    List<TwitchExtensionEventOptionGameResponse> Games);

public sealed record TwitchExtensionLinkedUserResponse(Guid Id, string DisplayName, string TwitchLogin);

/// <summary>
/// What the config views show. <c>LinkedUser</c> is null until the broadcaster
/// has signed in to Soulsjwa with the channel's Twitch account, and saving
/// requires it.
/// </summary>
public sealed record TwitchExtensionConfigurationResponse(
    string ChannelId,
    TwitchExtensionLinkedUserResponse? LinkedUser,
    TwitchExtensionPolicyResponse Policy,
    TwitchExtensionSettingsResponse Settings,
    TwitchExtensionEventResponse? ResolvedEvent,
    List<TwitchExtensionEventOptionResponse> Events);

public sealed record UpdateTwitchExtensionConfigurationRequest(
    Guid? EventId,
    string DefaultScope,
    Guid? PinnedEventGameId,
    bool HighlightChannelCompetitor = true,
    bool ShowTrialProgress = true);

/// <summary>
/// The extension backend service: what the Twitch-hosted front end calls with
/// the token Twitch issued to the viewer, plus the signed-in web app's view of
/// the same settings under <c>/me/twitch-extension</c>. The two share every
/// builder below so the panel, the Twitch config views and the app's Broadcast
/// tab can never disagree. Builders are <c>internal</c> for the integration
/// tests (see <see cref="ScoreboardEndpoint"/> for why).
/// </summary>
public class TwitchExtensionEndpoint(TwitchExtensionOptions options) : IEndpoint
{
    /// <summary>Route group of everything the Twitch-hosted front end calls.</summary>
    public const string RoutePrefix = ApiRoutes.Prefix + "/twitch-extension";

    /// <summary>The signed-in web app's own view of its channel's settings.</summary>
    public const string MeRoutePrefix = ApiRoutes.Prefix + "/me/twitch-extension";

    /// <summary>Broadcasters pick from the newest events; the picker is a short list, not a search.</summary>
    internal const int MaxEventOptions = 50;

    private const string UnknownGameName = "Unknown";

    /// <summary>Strong ETag: the first 32 hex chars of the body's SHA-256, enough to never collide in practice.</summary>
    private const int ETagHexLength = 32;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix)
            .RequireCors(CorsPolicies.TwitchExtension)
            .RequireRateLimiting(RateLimitPolicies.TwitchExtension);

        group.MapGet("/status", GetStatus)
            .WithName("GetTwitchExtensionStatus")
            .WithSummary("Whether this server backs a Twitch extension, and its client id.")
            .Produces<TwitchExtensionStatusResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        // With no client id and secret the server cannot verify a single
        // Twitch token, so nothing below is mapped: the routes 404 like any
        // unknown /api/v1 path instead of 401-ing every request.
        if (!options.IsConfigured)
            return;

        group.MapGet("/scoreboard", GetScoreboard)
            .WithName("GetTwitchExtensionScoreboard")
            .WithSummary("The board the calling viewer's channel shows: the broadcaster's chosen event, or the featured one.")
            .Produces<TwitchExtensionScoreboardResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .CacheOutput(PerTwitchChannelCachePolicy.PolicyName)
            .RequireAuthorization(TwitchExtensionAuth.ViewerPolicy);

        group.MapGet("/scoreboard/competitors/{userId:guid}", GetCompetitorDetail)
            .WithName("GetTwitchExtensionCompetitorDetail")
            .WithSummary("One competitor's objectives in the channel's event, for the expanded row.")
            .Produces<TwitchExtensionCompetitorDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .CacheOutput(PerTwitchChannelCachePolicy.PolicyName)
            .RequireAuthorization(TwitchExtensionAuth.ViewerPolicy);

        group.MapGet("/configuration", GetConfiguration)
            .WithName("GetTwitchExtensionConfiguration")
            .WithSummary("The channel's settings and the events the broadcaster can pick from.")
            .Produces<TwitchExtensionConfigurationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(TwitchExtensionAuth.BroadcasterPolicy);

        group.MapPut("/configuration", PutConfiguration)
            .WithName("UpdateTwitchExtensionConfiguration")
            .WithSummary("Saves the channel's settings. The broadcaster must have signed in to Soulsjwa with the channel's Twitch account.")
            .Produces<TwitchExtensionConfigurationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(TwitchExtensionAuth.BroadcasterPolicy);

        var me = app.MapGroup(MeRoutePrefix).RequireAuthorization();

        me.MapGet("/", GetMyConfiguration)
            .WithName("GetMyTwitchExtensionConfiguration")
            .WithSummary("The signed-in user's own channel's extension settings, as the Twitch config view would show them.")
            .Produces<TwitchExtensionConfigurationResponse>(StatusCodes.Status200OK);

        me.MapPut("/", PutMyConfiguration)
            .WithName("UpdateMyTwitchExtensionConfiguration")
            .WithSummary("Saves the signed-in user's own channel's extension settings.")
            .Produces<TwitchExtensionConfigurationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    internal static IResult GetStatus(TwitchExtensionOptions options) =>
        Results.Ok(new TwitchExtensionStatusResponse(options.IsConfigured, options.IsConfigured ? options.ClientId : null));

    private static async Task<IResult> GetScoreboard(
        HttpContext context,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var channelId = TwitchExtensionAuth.GetChannelId(principal);
        if (channelId is null)
            return Results.Unauthorized();

        var response = await BuildScoreboardAsync(channelId, db, ct);
        if (response.Event is not null)
            context.Items[PerTwitchChannelCachePolicy.ResolvedEventItemKey] = response.Event.Id;

        return WithETag(context, response);
    }

    private static async Task<IResult> GetCompetitorDetail(
        Guid userId,
        HttpContext context,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var channelId = TwitchExtensionAuth.GetChannelId(principal);
        if (channelId is null)
            return Results.Unauthorized();

        var resolved = await TwitchExtensionChannelResolver.ResolveAsync(channelId, db, ct);
        if (resolved.Event is null)
            return Results.Problem(detail: "This channel is not showing an event.", statusCode: StatusCodes.Status404NotFound);

        context.Items[PerTwitchChannelCachePolicy.ResolvedEventItemKey] = resolved.Event.Id;

        var detail = await BuildCompetitorDetailAsync(resolved.Event.Id, userId, db, ct);
        if (detail is null)
            return Results.Problem(detail: "Competitor not found in this event.", statusCode: StatusCodes.Status404NotFound);

        return WithETag(context, detail);
    }

    private static async Task<IResult> GetConfiguration(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var channelId = TwitchExtensionAuth.GetChannelId(principal);
        if (channelId is null)
            return Results.Unauthorized();

        var linkedUser = await FindLinkedUserAsync(channelId, db, ct);
        return Results.Ok(await BuildConfigurationAsync(channelId, linkedUser, db, ct));
    }

    private static async Task<IResult> PutConfiguration(
        UpdateTwitchExtensionConfigurationRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ITwitchExtensionPushNotifier notifier,
        CancellationToken ct)
    {
        var channelId = TwitchExtensionAuth.GetChannelId(principal);
        if (channelId is null)
            return Results.Unauthorized();

        var linkedUser = await FindLinkedUserAsync(channelId, db, ct);
        if (linkedUser is null)
            return Results.Problem(
                detail: "Sign in to Soulsjwa with this Twitch account once before configuring the extension.",
                statusCode: StatusCodes.Status403Forbidden);

        return await ApplyConfigurationAsync(channelId, linkedUser, request, db, audit, cache, notifier, ct);
    }

    internal static async Task<IResult> GetMyConfiguration(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == EventOwnership.GetUserId(principal), ct);
        if (user is null)
            return Results.Unauthorized();

        return Results.Ok(await BuildConfigurationAsync(user.TwitchId, user, db, ct));
    }

    internal static async Task<IResult> PutMyConfiguration(
        UpdateTwitchExtensionConfigurationRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ITwitchExtensionPushNotifier notifier,
        CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == EventOwnership.GetUserId(principal), ct);
        if (user is null)
            return Results.Unauthorized();

        return await ApplyConfigurationAsync(user.TwitchId, user, request, db, audit, cache, notifier, ct);
    }

    /// <summary>
    /// The broadcaster's Soulsjwa account, if they ever signed in here with
    /// the channel's Twitch account and are still allowed to. Saving settings
    /// requires one so the write is attributable and audited.
    /// </summary>
    internal static Task<User?> FindLinkedUserAsync(string channelId, AppDbContext db, CancellationToken ct) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.TwitchId == channelId && u.IsAllowlisted, ct);

    internal static async Task<TwitchExtensionScoreboardResponse> BuildScoreboardAsync(
        string channelId, AppDbContext db, CancellationToken ct)
    {
        var resolved = await TwitchExtensionChannelResolver.ResolveAsync(channelId, db, ct);
        var policy = ToPolicyResponse(resolved.Policy);
        var settings = ToSettingsResponse(resolved.Settings, resolved.Policy);
        if (resolved.Event is null)
            return new TwitchExtensionScoreboardResponse(channelId, policy, settings, null, null, [], []);

        var ev = resolved.Event;
        var games = await LoadGamesAsync(ev.Id, db, ct);
        var scoreboard = await ScoreboardEndpoint.BuildAsync(ev.Id, db, ct)
            // The event was read moments ago; a null here means it was
            // archived in between, which the next poll resolves.
            ?? new ScoreboardResponse([], ev.TieBreakMode.ToString());

        var channelCompetitorUserId = await db.EventCompetitors
            .AsNoTracking()
            .Where(c => c.EventId == ev.Id && c.User.TwitchId == channelId)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(ct);

        return new TwitchExtensionScoreboardResponse(
            channelId,
            policy,
            settings,
            ToEventResponse(ev, resolved.Source, games),
            channelCompetitorUserId,
            games,
            ProjectEntries(scoreboard));
    }

    internal static async Task<TwitchExtensionCompetitorDetailResponse?> BuildCompetitorDetailAsync(
        Guid eventId, Guid userId, AppDbContext db, CancellationToken ct)
    {
        var scoreboard = await ScoreboardEndpoint.BuildAsync(eventId, db, ct);
        var entry = scoreboard?.Entries.FirstOrDefault(e => e.UserId == userId);
        if (entry is null)
            return null;

        return new TwitchExtensionCompetitorDetailResponse(
            entry.UserId,
            entry.DisplayName,
            entry.Games
                .Select(g => new TwitchExtensionCompetitorGameDetailResponse(
                    g.EventGameId, g.GameName, g.IsEnabled, g.IsTrialActive, g.Objectives))
                .ToList());
    }

    internal static async Task<TwitchExtensionConfigurationResponse> BuildConfigurationAsync(
        string channelId, User? linkedUser, AppDbContext db, CancellationToken ct)
    {
        var resolved = await TwitchExtensionChannelResolver.ResolveAsync(channelId, db, ct);
        var linkedUserId = linkedUser?.Id;
        var isLinked = linkedUserId is not null;

        var events = await db.Events
            .AsNoTracking()
            .OrderByDescending(e => e.IsFeatured)
            .ThenByDescending(e => e.IsStarted)
            .ThenByDescending(e => e.CreatedAt)
            .Take(MaxEventOptions)
            .Select(e => new TwitchExtensionEventOptionResponse(
                e.Id,
                e.Name,
                e.IsStarted,
                e.IsFeatured,
                isLinked && e.Competitors.Any(c => c.UserId == linkedUserId),
                e.EventGames
                    .OrderBy(g => g.SortOrder)
                    .ThenBy(g => g.Id)
                    .Select(g => new TwitchExtensionEventOptionGameResponse(
                        g.Id,
                        g.CustomGameName ?? (g.KnownGame != null ? g.KnownGame.Name : UnknownGameName),
                        g.IsEnabled))
                    .ToList()))
            .ToListAsync(ct);

        TwitchExtensionEventResponse? resolvedEvent = null;
        if (resolved.Event is not null)
            resolvedEvent = ToEventResponse(resolved.Event, resolved.Source, await LoadGamesAsync(resolved.Event.Id, db, ct));

        return new TwitchExtensionConfigurationResponse(
            channelId,
            linkedUser is null ? null : new TwitchExtensionLinkedUserResponse(linkedUser.Id, linkedUser.DisplayName, linkedUser.TwitchLogin),
            ToPolicyResponse(resolved.Policy),
            ToSettingsResponse(resolved.Settings, resolved.Policy),
            resolvedEvent,
            events);
    }

    /// <summary>
    /// Validates and upserts a channel's settings on behalf of
    /// <paramref name="actor"/>, the linked Soulsjwa user. A pinned game must
    /// belong to the event the channel will actually show — the explicit pick
    /// when there is one, else the featured event — so the panel never opens
    /// on a game of some other event.
    /// </summary>
    internal static async Task<IResult> ApplyConfigurationAsync(
        string channelId,
        User actor,
        UpdateTwitchExtensionConfigurationRequest request,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ITwitchExtensionPushNotifier notifier,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var policy = await TwitchExtensionChannelResolver.LoadPolicyAsync(db, ct);

        if (!Enum.TryParse<TwitchExtensionScope>(request.DefaultScope, ignoreCase: false, out var scope))
            errors[nameof(request.DefaultScope)] =
                [$"DefaultScope must be one of: {string.Join(", ", Enum.GetNames<TwitchExtensionScope>())}."];

        Event? targetEvent = null;
        if (request.EventId is not null && !policy.AllowChannelEventChoice)
            errors[nameof(request.EventId)] = ["An admin has set every channel to follow the featured event."];
        else if (request.EventId is { } eventId)
        {
            targetEvent = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
            if (targetEvent is null)
                errors[nameof(request.EventId)] = ["Event not found."];
        }

        Guid? pinnedEventGameId = null;
        if (errors.Count == 0 && scope == TwitchExtensionScope.PinnedGame)
        {
            var shownEvent = targetEvent ?? await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.IsFeatured, ct);
            if (request.PinnedEventGameId is not { } pinned)
                errors[nameof(request.PinnedEventGameId)] = ["A pinned game is required for the PinnedGame scope."];
            else if (shownEvent is null
                || !await db.EventGames.AnyAsync(g => g.Id == pinned && g.EventId == shownEvent.Id, ct))
                errors[nameof(request.PinnedEventGameId)] = ["The pinned game must belong to the event the channel shows."];
            else
                pinnedEventGameId = pinned;
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var nowUtc = DateTime.UtcNow;
        var settings = await db.TwitchExtensionChannelSettings.FirstOrDefaultAsync(s => s.ChannelId == channelId, ct);
        var before = settings is null ? null : Snapshot(settings);
        if (settings is null)
        {
            settings = new TwitchExtensionChannelSettings { ChannelId = channelId };
            db.TwitchExtensionChannelSettings.Add(settings);
        }

        settings.EventId = targetEvent?.Id;
        settings.DefaultScope = scope;
        settings.PinnedEventGameId = pinnedEventGameId;
        settings.HighlightChannelCompetitor = request.HighlightChannelCompetitor;
        settings.ShowTrialProgress = request.ShowTrialProgress;
        settings.UpdatedById = actor.Id;
        settings.UpdatedAt = nowUtc;

        audit.Log(db, AuditEventTypes.TwitchExtensionConfigurationUpdated, actor.Id,
            eventId: settings.EventId,
            eventGameId: settings.PinnedEventGameId,
            before: before,
            after: Snapshot(settings));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                detail: "These settings were changed concurrently. Reload and try again.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await cache.EvictByTagAsync(CacheTags.TwitchExtensionChannel(channelId), ct);
        notifier.ConfigurationChanged(channelId);

        return Results.Ok(await BuildConfigurationAsync(channelId, actor, db, ct));
    }

    internal static List<TwitchExtensionEntryResponse> ProjectEntries(ScoreboardResponse scoreboard) =>
        scoreboard.Entries
            .Select(e => new TwitchExtensionEntryResponse(
                e.UserId,
                e.DisplayName,
                e.TwitchLogin,
                e.ProfileImageUrl,
                e.IsLive,
                e.Rank,
                e.TotalScore,
                e.CompletedCount,
                e.FailedCount,
                e.IsFinished,
                e.Status,
                e.LastCompletedAt,
                e.TotalInGameTimeMs,
                e.Games
                    .Select(g => new TwitchExtensionEntryGameResponse(
                        g.EventGameId,
                        g.Score,
                        g.CompletedCount,
                        g.FailedCount,
                        g.Objectives.Max(o => o.CompletedAt),
                        g.IsTrialActive,
                        g.Trial))
                    .ToList()))
            .ToList();

    internal static TwitchExtensionPolicyResponse ToPolicyResponse(TwitchExtensionSettings policy) =>
        new(
            policy.AllowChannelEventChoice,
            policy.AllowViewerScopeSwitch,
            policy.DefaultScope.ToString(),
            policy.DefaultHighlightChannelCompetitor,
            policy.DefaultShowTrialProgress);

    /// <summary>
    /// A channel's effective settings: its own row, with the pick suppressed
    /// while the admin disallows picks (the row keeps it for when they are
    /// allowed again), or the admin's defaults while it has no row.
    /// </summary>
    internal static TwitchExtensionSettingsResponse ToSettingsResponse(
        TwitchExtensionChannelSettings? settings, TwitchExtensionSettings policy) =>
        settings is null
            ? new TwitchExtensionSettingsResponse(
                null,
                policy.DefaultScope.ToString(),
                null,
                policy.DefaultHighlightChannelCompetitor,
                policy.DefaultShowTrialProgress)
            : new TwitchExtensionSettingsResponse(
                policy.AllowChannelEventChoice ? settings.EventId : null,
                settings.DefaultScope.ToString(),
                settings.PinnedEventGameId,
                settings.HighlightChannelCompetitor,
                settings.ShowTrialProgress);

    private static TwitchExtensionEventResponse ToEventResponse(
        Event ev, TwitchExtensionEventSource source, IReadOnlyList<TwitchExtensionGameResponse> games) =>
        new(
            ev.Id,
            ev.Name,
            ev.UrlAlias,
            ev.IsStarted,
            ev.IsFeatured,
            ev.TieBreakMode.ToString(),
            games.FirstOrDefault(g => g.IsEnabled)?.EventGameId,
            source.ToString());

    private static Task<List<TwitchExtensionGameResponse>> LoadGamesAsync(Guid eventId, AppDbContext db, CancellationToken ct) =>
        db.EventGames
            .AsNoTracking()
            .Where(g => g.EventId == eventId)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Id)
            .Select(g => new TwitchExtensionGameResponse(
                g.Id,
                g.CustomGameName ?? (g.KnownGame != null ? g.KnownGame.Name : UnknownGameName),
                g.IsEnabled,
                g.SortOrder,
                g.Objectives.Count))
            .ToListAsync(ct);

    private static object Snapshot(TwitchExtensionChannelSettings settings) => new
    {
        settings.ChannelId,
        settings.EventId,
        DefaultScope = settings.DefaultScope.ToString(),
        settings.PinnedEventGameId,
        settings.HighlightChannelCompetitor,
        settings.ShowTrialProgress,
    };

    /// <summary>
    /// Serialises once, hashes the bytes into a strong <c>ETag</c>, and answers
    /// a matching <c>If-None-Match</c> with a bodiless 304. The output cache
    /// does the same for entries it serves, so both the hot and the cold path
    /// cost an unchanged poll nothing but headers. The body has no
    /// timestamp on purpose — it would change the tag every build.
    /// </summary>
    private static IResult WithETag<T>(HttpContext context, T body)
    {
        var serializerOptions = context.RequestServices
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, serializerOptions);
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(SHA256.HashData(bytes))[..ETagHexLength]}\"");
        context.Response.Headers.ETag = etag.ToString();

        if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatch)
            && EntityTagHeaderValue.TryParseList(ifNoneMatch, out var candidates)
            && candidates.Any(c => c.Equals(EntityTagHeaderValue.Any) || c.Compare(etag, useStrongComparison: true)))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.Bytes(bytes, "application/json; charset=utf-8");
    }
}
