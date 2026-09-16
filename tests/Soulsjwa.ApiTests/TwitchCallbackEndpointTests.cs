using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// TwitchCallbackEndpoint used to return a bare
/// Results.BadRequest("Invalid state parameter") string body for a missing or
/// mismatched OAuth state cookie — now RFC 7807 like every other error.
/// </summary>
public class TwitchCallbackEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Callback_WithNoStateCookie_Returns400ProblemJson()
    {
        var response = await Client.GetAsync("/api/v1/auth/twitch/callback?code=abc&state=whatever");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        body!.Detail.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Twitch sends the user back with <c>?error=access_denied</c> and no
    /// <c>code</c> when they cancel on the consent screen. That used to be a
    /// raw 400 for the missing parameter; it is an ordinary outcome and lands
    /// on the SPA's callback page like the other sign-in failures.
    /// </summary>
    [Theory]
    [InlineData("?error=access_denied&error_description=The+user+denied+access&state=whatever", "access_denied")]
    [InlineData("?state=whatever", "twitch_unavailable")]
    [InlineData("?error=server_error", "twitch_unavailable")]
    public async Task Callback_WhenTwitchReportsAnErrorOrNoCode_RedirectsToTheFrontend(string query, string expectedError)
    {
        using var client = Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/auth/twitch/callback" + query);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be($"http://localhost:5173/auth/callback?error={expectedError}");
    }

    private sealed record ProblemDetailsDto(string? Detail);
}
