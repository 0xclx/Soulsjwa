using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Users.Endpoints;

public class DeleteApiKeyEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete(ApiRoutes.Prefix + "/users/me/api-keys/{id:guid}", Handle)
            .WithName("DeleteApiKey")
            .WithSummary("Revokes an API key")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        ILogger<DeleteApiKeyEndpoint> logger,
        CancellationToken ct)
    {
        var userId = EventOwnership.GetUserId(principal);
        var apiKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.UserId == userId, ct);
        if (apiKey is null) return Results.NotFound();

        apiKey.IsRevoked = true;
        await db.SaveChangesAsync(ct);
        logger.ApiKeyRevoked(apiKey.Id, apiKey.KeyPrefix, userId);
        return Results.NoContent();
    }
}
