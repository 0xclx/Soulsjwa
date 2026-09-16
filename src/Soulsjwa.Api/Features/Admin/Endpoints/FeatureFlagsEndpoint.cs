using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Admin.Entities;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Admin.Endpoints;

public sealed record FeatureFlagResponse(string Key, bool Enabled);
public sealed record UpdateFeatureFlagRequest(bool Enabled);

public class FeatureFlagsEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/admin/feature-flags").RequireAdmin();

        group.MapGet("/{key}", Get)
            .WithName("AdminGetFeatureFlag")
            .WithSummary("Gets a feature flag (admin only)")
            .Produces<FeatureFlagResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPut("/{key}", Update)
            .WithName("AdminUpdateFeatureFlag")
            .WithSummary("Enables or disables a feature flag (admin only)")
            .Produces<FeatureFlagResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    private static async Task<IResult> Get(
        string key,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();
        if (!IsKnown(key)) return NotFound();

        var enabled = await db.FeatureFlags
            .Where(flag => flag.Key == key)
            .Select(flag => flag.Enabled)
            .SingleOrDefaultAsync(ct);
        return Results.Ok(new FeatureFlagResponse(key, enabled));
    }

    private static async Task<IResult> Update(
        string key,
        UpdateFeatureFlagRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();
        if (!IsKnown(key)) return NotFound();

        var flag = await db.FeatureFlags.FindAsync([key], ct);
        var previousValue = flag?.Enabled ?? false;
        if (flag is null)
        {
            flag = new FeatureFlag { Key = key };
            db.FeatureFlags.Add(flag);
        }

        flag.Enabled = request.Enabled;
        audit.Log(
            db,
            AuditEventTypes.FeatureFlagUpdated,
            EventOwnership.GetUserId(principal),
            before: new { Key = key, Enabled = previousValue },
            after: new { Key = key, request.Enabled });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new FeatureFlagResponse(key, flag.Enabled));
    }

    private static bool IsKnown(string key) =>
        string.Equals(key, FeatureFlagKeys.MyEventsQuickComplete, StringComparison.Ordinal);

    private static IResult NotFound() => Results.Problem(
        detail: "Feature flag not found.",
        statusCode: StatusCodes.Status404NotFound);
}
