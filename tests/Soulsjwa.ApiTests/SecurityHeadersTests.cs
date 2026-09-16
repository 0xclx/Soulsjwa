using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

public class SecurityHeadersTests : ApiTestBase
{
    // A bare `https:` scheme token (as opposed to `https://specific-host.example`)
    // would allow any HTTPS origin — exactly what img-src must no longer do.
    private static readonly Regex BareHttpsToken = new(@"(?<![:/])\bhttps:(?!//)", RegexOptions.Compiled);

    [Fact]
    public async Task ContentSecurityPolicy_HasNoBareHttpsToken()
    {
        var response = await Client.GetAsync("/api/v1/games");

        response.Headers.TryGetValues("Content-Security-Policy", out var values).Should().BeTrue();
        var csp = values!.Single();

        BareHttpsToken.IsMatch(csp).Should().BeFalse(
            "no directive may fall back to a bare https: wildcard scheme");
        csp.Should().Contain("img-src 'self' data: https://static-cdn.jtvnw.net");
    }

    [Fact]
    public async Task Framing_IsAllowedForTheSiteItselfOnly()
    {
        // The overlay designer previews the overlay route in an iframe of the
        // app's own page; any other origin stays locked out.
        var response = await Client.GetAsync("/api/v1/games");

        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("frame-ancestors 'self'");
        response.Headers.GetValues("X-Frame-Options").Single().Should().Be("SAMEORIGIN");
    }
}
