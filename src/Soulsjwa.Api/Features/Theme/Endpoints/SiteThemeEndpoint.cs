using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Theme.Entities;
using Soulsjwa.Api.Features.Theme.Services;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Theme.Endpoints;

public sealed record SiteThemeResponse(
    Guid? BackgroundAssetId,
    string? BackgroundUrl,
    string BackgroundTreatment,
    string Font,
    string LightDefault,
    string LightAccent,
    string LightDanger,
    string LightInfo,
    string LightSuccess,
    string LightHighlight,
    string DarkDefault,
    string DarkAccent,
    string DarkDanger,
    string DarkInfo,
    string DarkSuccess,
    string DarkHighlight,
    DateTime UpdatedAt);

public sealed record UpdateSiteThemeRequest(
    Guid? BackgroundAssetId,
    string BackgroundTreatment,
    string Font,
    string LightDefault,
    string LightAccent,
    string LightDanger,
    string LightInfo,
    string LightSuccess,
    string LightHighlight,
    string DarkDefault,
    string DarkAccent,
    string DarkDanger,
    string DarkInfo,
    string DarkSuccess,
    string DarkHighlight);

/// <summary>Site-wide theme: public read, admin-only write.</summary>
public class SiteThemeEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/theme");

        group.MapGet("/", GetTheme)
            .WithName("GetSiteTheme")
            .WithSummary("Gets the site-wide theme")
            .Produces<SiteThemeResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        group.MapPut("/", UpdateTheme)
            .WithName("UpdateSiteTheme")
            .WithSummary("Sets the site-wide theme (admin only)")
            .Produces<SiteThemeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAdmin();
    }

    private static async Task<IResult> GetTheme(AppDbContext db, HttpContext context, CancellationToken ct)
    {
        var theme = await db.SiteThemes.SingleAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.ToETag(ConcurrencyToken.GetXmin(db, theme));
        return Results.Ok(MapToResponse(theme));
    }

    private static async Task<IResult> UpdateTheme(
        UpdateSiteThemeRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        HttpContext context,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var errors = new Dictionary<string, string[]>();

        if (!Enum.TryParse<BackgroundTreatment>(request.BackgroundTreatment, ignoreCase: false, out var treatment))
        {
            errors["backgroundTreatment"] =
                [$"backgroundTreatment must be one of: {string.Join(", ", Enum.GetNames<BackgroundTreatment>())}."];
        }

        if (!SiteThemeValidator.IsAllowedFont(request.Font))
        {
            errors["font"] = [$"font must be one of: {string.Join(", ", Enum.GetNames<SiteFont>())}."];
        }

        if (request.BackgroundAssetId is { } assetId &&
            !await db.MediaAssets.AnyAsync(a => a.Id == assetId, ct))
        {
            errors["backgroundAssetId"] = ["backgroundAssetId does not reference an existing media asset."];
        }

        var candidatePalette = new SiteTheme
        {
            LightDefault = request.LightDefault,
            LightAccent = request.LightAccent,
            LightDanger = request.LightDanger,
            LightInfo = request.LightInfo,
            LightSuccess = request.LightSuccess,
            LightHighlight = request.LightHighlight,
            DarkDefault = request.DarkDefault,
            DarkAccent = request.DarkAccent,
            DarkDanger = request.DarkDanger,
            DarkInfo = request.DarkInfo,
            DarkSuccess = request.DarkSuccess,
            DarkHighlight = request.DarkHighlight,
        };
        var paletteError = SiteThemeValidator.Validate(candidatePalette);
        if (paletteError is not null) errors["palette"] = [paletteError];

        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var theme = await db.SiteThemes.SingleAsync(ct);
        ConcurrencyToken.ApplyIfMatch(db, theme, context.Request.Headers.IfMatch);
        var before = MapToResponse(theme);

        theme.BackgroundAssetId = request.BackgroundAssetId;
        theme.BackgroundTreatment = treatment;
        theme.Font = Enum.Parse<SiteFont>(request.Font, ignoreCase: false);
        theme.LightDefault = request.LightDefault;
        theme.LightAccent = request.LightAccent;
        theme.LightDanger = request.LightDanger;
        theme.LightInfo = request.LightInfo;
        theme.LightSuccess = request.LightSuccess;
        theme.LightHighlight = request.LightHighlight;
        theme.DarkDefault = request.DarkDefault;
        theme.DarkAccent = request.DarkAccent;
        theme.DarkDanger = request.DarkDanger;
        theme.DarkInfo = request.DarkInfo;
        theme.DarkSuccess = request.DarkSuccess;
        theme.DarkHighlight = request.DarkHighlight;
        theme.UpdatedAt = DateTime.UtcNow;

        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.SiteThemeUpdated, actorId, before: before, after: MapToResponse(theme));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                detail: "This record was modified by someone else. Reload and reapply your change.",
                statusCode: StatusCodes.Status409Conflict);
        }

        context.Response.Headers.ETag = ConcurrencyToken.ToETag(ConcurrencyToken.GetXmin(db, theme));
        return Results.Ok(MapToResponse(theme));
    }

    private static SiteThemeResponse MapToResponse(SiteTheme theme) => new(
        theme.BackgroundAssetId,
        theme.BackgroundAssetId is { } assetId ? $"/api/v1/media/{assetId}" : null,
        theme.BackgroundTreatment.ToString(),
        theme.Font.ToString(),
        theme.LightDefault,
        theme.LightAccent,
        theme.LightDanger,
        theme.LightInfo,
        theme.LightSuccess,
        theme.LightHighlight,
        theme.DarkDefault,
        theme.DarkAccent,
        theme.DarkDanger,
        theme.DarkInfo,
        theme.DarkSuccess,
        theme.DarkHighlight,
        theme.UpdatedAt);
}
