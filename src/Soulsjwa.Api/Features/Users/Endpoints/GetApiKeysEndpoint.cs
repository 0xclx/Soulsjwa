using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Users.Endpoints;

public sealed record ApiKeyResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsRevoked);

public class GetApiKeysEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/users/me/api-keys", Handle)
            .WithName("GetApiKeys")
            .WithSummary("Lists the current user's API keys")
            .Produces<List<ApiKeyResponse>>(StatusCodes.Status200OK)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handle(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct,
        bool includeRevoked = false)
    {
        var userId = EventOwnership.GetUserId(principal);
        var query = db.ApiKeys.Where(k => k.UserId == userId);
        if (!includeRevoked)
            query = query.Where(k => !k.IsRevoked);

        var keys = await query
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyResponse(k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.ExpiresAt, k.LastUsedAt, k.IsRevoked))
            .ToListAsync(ct);

        return Results.Ok(keys);
    }
}
