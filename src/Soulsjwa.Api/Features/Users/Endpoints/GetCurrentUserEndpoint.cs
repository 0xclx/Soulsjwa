using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Users.Endpoints;

public sealed record UserResponse(
    Guid Id,
    string TwitchLogin,
    string DisplayName,
    string? Email,
    string? ProfileImageUrl,
    DateTime CreatedAt,
    string Role,
    bool IsAllowlisted,
    string TwitchDisplayName,
    string? DisplayNameOverride)
{
    public static UserResponse From(User user) => new(
        user.Id,
        user.TwitchLogin,
        user.DisplayName,
        user.Email,
        user.ProfileImageUrl,
        user.CreatedAt,
        user.Role.ToString(),
        user.IsAllowlisted,
        user.TwitchDisplayName,
        user.DisplayNameOverride);
}

public class GetCurrentUserEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/users/me", Handle)
            .WithName("GetCurrentUser")
            .WithSummary("Gets the currently authenticated user")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handle(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = EventOwnership.GetUserId(principal);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Results.NotFound();

        return Results.Ok(UserResponse.From(user));
    }
}
