using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Auth;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;

namespace Soulsjwa.Api.Features.Auth.Endpoints;

public class TwitchCallbackEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/auth/twitch/callback", Handle)
            .WithName("TwitchCallback")
            .WithSummary("Handles Twitch OAuth2 callback")
            .WithDescription("Redirects to the frontend's /auth/callback page. A user cancelling on Twitch (?error=access_denied) is redirected there with ?error=access_denied; only a missing or mismatched OAuth state is a 400.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        HttpContext httpContext,
        TwitchAuthService twitchAuth,
        OAuthStateCookie stateCookie,
        JwtTokenService jwtService,
        IConfiguration config,
        ILogger<TwitchCallbackEndpoint> logger,
        CancellationToken ct,
        string? code = null,
        string? state = null,
        string? error = null)
    {
        var frontendUrl = config["Frontend:Url"] ?? "http://localhost:5173";

        // Twitch redirects back with ?error=access_denied (and no code) when
        // the user cancels on the consent screen. That is a normal outcome, so
        // it lands on the SPA's callback page like every other sign-in failure
        // rather than as a 400 for the missing `code` parameter. The state
        // cookie is consumed either way so it cannot be replayed.
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            if (state is not null) stateCookie.TryConsume(httpContext, state);
            var reason = error is "access_denied" ? "access_denied" : "twitch_unavailable";
            return Results.Redirect($"{frontendUrl}/auth/callback?error={reason}");
        }

        if (string.IsNullOrEmpty(state) || !stateCookie.TryConsume(httpContext, state))
        {
            logger.OAuthStateMismatch();
            return Results.Problem(detail: "Invalid state parameter", statusCode: StatusCodes.Status400BadRequest);
        }

        var twitchToken = await twitchAuth.ExchangeCodeAsync(code, ct);
        if (twitchToken is null)
            return Results.Redirect($"{frontendUrl}/auth/callback?error=twitch_unavailable");

        var twitchUser = await twitchAuth.GetUserInfoAsync(twitchToken.AccessToken, ct);
        if (twitchUser is null)
            return Results.Redirect($"{frontendUrl}/auth/callback?error=twitch_unavailable");

        User user;
        try
        {
            user = await twitchAuth.UpsertUserAsync(twitchUser, ct);
        }
        catch (NotAllowlistedException ex)
        {
            var qs = $"error=not_allowlisted&login={Uri.EscapeDataString(ex.TwitchLogin)}";
            return Results.Redirect($"{frontendUrl}/auth/callback?{qs}");
        }
        var (rawToken, refreshToken) = await jwtService.GenerateRefreshTokenAsync(user.Id, ct);

        httpContext.Response.Cookies.Append(
            RefreshTokenCookie.Name,
            rawToken,
            RefreshTokenCookie.CreateOptions(refreshToken.ExpiresAt));

        return Results.Redirect($"{frontendUrl}/auth/callback");
    }
}
