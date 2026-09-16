using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Auth;
using Soulsjwa.Api.Infrastructure.Auth;

namespace Soulsjwa.Api.Features.Auth.Endpoints;

public class RevokeTokenEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Prefix + "/auth/revoke", Handle)
            .WithName("RevokeToken")
            .WithSummary("Revokes a refresh token")
            .Produces(StatusCodes.Status200OK)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        HttpContext httpContext,
        JwtTokenService jwtService,
        CancellationToken ct)
    {
        if (httpContext.Request.Cookies.TryGetValue(RefreshTokenCookie.Name, out var refreshTokenValue) &&
            !string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            await jwtService.RevokeRefreshTokenAsync(refreshTokenValue, ct);
        }

        httpContext.Response.Cookies.Delete(RefreshTokenCookie.Name, RefreshTokenCookie.CreateDeletionOptions());
        return Results.Ok();
    }
}
