using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.TwitchExtension.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Services;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.TwitchExtension.Endpoints;

/// <summary>Whether the built front end is part of this deployment, and the origin the download will point it at.</summary>
public sealed record TwitchExtensionBundleInfoResponse(bool Available, int FileCount, string ApiUrl);

/// <summary>
/// Everything the admin page shows: the credentials' state (read-only — they
/// come from configuration), the bundle, and the extension-wide rules.
/// </summary>
public sealed record TwitchExtensionAdminResponse(
    bool Configured,
    string? ClientId,
    bool CanPush,
    string? ExtensionOrigin,
    string? LocalTestOrigin,
    TwitchExtensionBundleInfoResponse Bundle,
    TwitchExtensionPolicyResponse Settings,
    DateTime? UpdatedAt,
    Guid? UpdatedById);

public sealed record UpdateTwitchExtensionSettingsRequest(
    bool AllowChannelEventChoice,
    bool AllowViewerScopeSwitch,
    string DefaultScope,
    bool DefaultHighlightChannelCompetitor,
    bool DefaultShowTrialProgress);

/// <summary>
/// The admin page's side of the Twitch extension: status, the rules that
/// apply to every channel, and the zip to upload to Twitch. Handlers are
/// <c>internal</c> for the integration tests.
/// </summary>
public class TwitchExtensionAdminEndpoint : IEndpoint
{
    public const string RoutePrefix = ApiRoutes.Prefix + "/admin/twitch-extension";

    /// <summary>Where the extension calls this deployment: an explicit override, else the site's public URL.</summary>
    public const string ApiUrlKey = "TwitchExtension:ApiUrl";
    private const string FrontendUrlKey = "Frontend:Url";

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix).RequireAdmin();

        group.MapGet("/", Get)
            .WithName("AdminGetTwitchExtension")
            .WithSummary("Status, bundle and extension-wide rules of the Twitch extension (admin only)")
            .Produces<TwitchExtensionAdminResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/settings", UpdateSettings)
            .WithName("AdminUpdateTwitchExtensionSettings")
            .WithSummary("Sets the rules that apply to every channel showing the extension (admin only)")
            .Produces<TwitchExtensionAdminResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/bundle", DownloadBundle)
            .WithName("AdminDownloadTwitchExtensionBundle")
            .WithSummary("The zip to upload to the Twitch developer console, with this deployment's API origin written in (admin only)")
            .Produces(StatusCodes.Status200OK, contentType: "application/zip")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    internal static async Task<IResult> Get(
        ClaimsPrincipal principal,
        AppDbContext db,
        HttpContext context,
        TwitchExtensionOptions options,
        TwitchExtensionBundle bundle,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var settings = await db.TwitchExtensionSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is not null)
            context.Response.Headers.ETag = ConcurrencyToken.ToETag(ConcurrencyToken.GetXmin(db, settings));

        return Results.Ok(Build(settings, options, bundle, configuration));
    }

    internal static async Task<IResult> UpdateSettings(
        UpdateTwitchExtensionSettingsRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        HttpContext context,
        IAuditService audit,
        IOutputCacheStore cache,
        TwitchExtensionOptions options,
        TwitchExtensionBundle bundle,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        if (!Enum.TryParse<TwitchExtensionScope>(request.DefaultScope, ignoreCase: false, out var scope)
            || scope == TwitchExtensionScope.PinnedGame)
        {
            // A pinned game belongs to one event; a default for every channel cannot name one.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.DefaultScope)] =
                    [$"DefaultScope must be {nameof(TwitchExtensionScope.AllGames)} or {nameof(TwitchExtensionScope.ActiveGame)}."],
            });
        }

        var settings = await db.TwitchExtensionSettings.FirstOrDefaultAsync(ct);
        var before = settings is null ? null : Snapshot(settings);
        if (settings is null)
        {
            settings = new TwitchExtensionSettings();
            db.TwitchExtensionSettings.Add(settings);
        }
        else
        {
            ConcurrencyToken.ApplyIfMatch(db, settings, context.Request.Headers.IfMatch);
        }

        settings.AllowChannelEventChoice = request.AllowChannelEventChoice;
        settings.AllowViewerScopeSwitch = request.AllowViewerScopeSwitch;
        settings.DefaultScope = scope;
        settings.DefaultHighlightChannelCompetitor = request.DefaultHighlightChannelCompetitor;
        settings.DefaultShowTrialProgress = request.DefaultShowTrialProgress;
        settings.UpdatedById = EventOwnership.GetUserId(principal);
        settings.UpdatedAt = DateTime.UtcNow;

        audit.Log(db, AuditEventTypes.TwitchExtensionSettingsUpdated, settings.UpdatedById.Value,
            before: before, after: Snapshot(settings));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                detail: "The extension settings were changed concurrently. Reload and try again.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Every channel's cached board embeds these rules.
        await cache.EvictByTagAsync(CacheTags.TwitchExtensionAll, ct);

        context.Response.Headers.ETag = ConcurrencyToken.ToETag(ConcurrencyToken.GetXmin(db, settings));
        return Results.Ok(Build(settings, options, bundle, configuration));
    }

    internal static IResult DownloadBundle(
        ClaimsPrincipal principal,
        HttpContext context,
        TwitchExtensionBundle bundle,
        IConfiguration configuration)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        if (!bundle.IsAvailable)
            return Results.Problem(
                detail: $"The extension bundle is not part of this deployment (looked in {bundle.RootPath}). " +
                        $"The Docker image builds it; elsewhere run `npm run build:twitch` and point {TwitchExtensionBundle.PathKey} at the output.",
                statusCode: StatusCodes.Status404NotFound);

        var apiUrl = ResolveApiUrl(configuration);
        return Results.Stream(
            stream => bundle.WriteZipAsync(stream, apiUrl, context.RequestAborted),
            contentType: "application/zip",
            fileDownloadName: TwitchExtensionBundle.ZipFileName);
    }

    /// <summary>The origin written into the downloaded bundle: <c>TwitchExtension:ApiUrl</c> when set, else <c>Frontend:Url</c>, without a trailing slash.</summary>
    internal static string ResolveApiUrl(IConfiguration configuration) =>
        (configuration[ApiUrlKey] ?? configuration[FrontendUrlKey] ?? string.Empty).Trim().TrimEnd('/');

    private static TwitchExtensionAdminResponse Build(
        TwitchExtensionSettings? settings,
        TwitchExtensionOptions options,
        TwitchExtensionBundle bundle,
        IConfiguration configuration)
    {
        var policy = settings ?? TwitchExtensionSettings.Defaults();
        return new TwitchExtensionAdminResponse(
            options.IsConfigured,
            options.IsConfigured ? options.ClientId : null,
            options.CanPush,
            options.IsConfigured ? options.AllowedOrigins[0] : null,
            options.LocalTestOrigin,
            new TwitchExtensionBundleInfoResponse(bundle.IsAvailable, bundle.FileCount, ResolveApiUrl(configuration)),
            TwitchExtensionEndpoint.ToPolicyResponse(policy),
            settings?.UpdatedAt,
            settings?.UpdatedById);
    }

    private static object Snapshot(TwitchExtensionSettings settings) => new
    {
        settings.AllowChannelEventChoice,
        settings.AllowViewerScopeSwitch,
        DefaultScope = settings.DefaultScope.ToString(),
        settings.DefaultHighlightChannelCompetitor,
        settings.DefaultShowTrialProgress,
    };
}
