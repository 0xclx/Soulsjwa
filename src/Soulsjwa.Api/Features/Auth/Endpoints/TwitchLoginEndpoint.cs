using System.Security.Cryptography;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Infrastructure.Auth;

namespace Soulsjwa.Api.Features.Auth.Endpoints;

public class TwitchLoginEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/auth/twitch/login", Handle)
            .WithName("TwitchLogin")
            .WithSummary("Initiates Twitch OAuth2 login flow")
            .Produces(StatusCodes.Status302Found)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AllowAnonymous();
    }

    private static IResult Handle(TwitchAuthService twitchAuth, OAuthStateCookie stateCookie, HttpContext httpContext)
    {
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        stateCookie.Issue(httpContext.Response, state);
        var url = twitchAuth.GetAuthorizationUrl(state);
        return Results.Redirect(url);
    }
}
