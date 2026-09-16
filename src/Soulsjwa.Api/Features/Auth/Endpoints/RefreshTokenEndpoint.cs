using System.Data;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Auth;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Auth.Endpoints;

public sealed record TokenResponse(string AccessToken);

public class RefreshTokenEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Prefix + "/auth/refresh", Handle)
            .WithName("RefreshToken")
            .WithSummary("Refreshes an access token using a refresh token")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        HttpContext httpContext,
        JwtTokenService jwtService,
        AppDbContext db,
        CancellationToken ct)
    {
        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenCookie.Name, out var refreshTokenValue) ||
            string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            return Results.Unauthorized();
        }

        var (user, oldRefreshToken) = await jwtService.ValidateRefreshTokenAsync(refreshTokenValue, ct);
        if (user is null || oldRefreshToken is null)
            return Results.Unauthorized();

        // The revoke, the new token's issue, and the cookie write must commit
        // together: a crash or lost response between them must not leave a
        // revoked-but-unreplaced token, which would look like theft to the
        // reuse-detection logic on the client's next refresh.
        // Disposed on every exit; an early return or an exception rolls back.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        // Conditional update: exactly one of two concurrent rotations of the
        // same cookie claims the token. The loser aborts with 401 and mints
        // nothing, rather than both racers seeing "still active" and each
        // issuing a fresh chain — which would silently defeat reuse detection.
        var claimed = await jwtService.TryRevokeForRotationAsync(oldRefreshToken.Id, ct);
        if (!claimed)
            return Results.Unauthorized();

        var newAccessToken = jwtService.GenerateAccessToken(user);
        var (rawToken, newRefreshToken) = await jwtService.GenerateRefreshTokenAsync(user.Id, ct);

        httpContext.Response.Cookies.Append(
            RefreshTokenCookie.Name,
            rawToken,
            RefreshTokenCookie.CreateOptions(newRefreshToken.ExpiresAt));

        await tx.CommitAsync(ct);
        return Results.Ok(new TokenResponse(newAccessToken));
    }
}
