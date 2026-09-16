using System.Net;
using System.Net.Http;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// An unmatched /api/v1/* route must terminate as a real 404, not fall through
/// to the SPA shell (which would return 200 text/html).
/// </summary>
public class ApiNotFoundTests : ApiTestBase
{
    [Fact]
    public async Task UnmatchedApiRoute_Returns404ProblemJson()
    {
        var response = await Client.GetAsync("/api/v1/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task RealApiRoute_StillReturns200Json()
    {
        var response = await Client.GetAsync("/api/v1/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task UnmatchedNonApiRoute_StillFallsBackToTheSpaShell()
    {
        var response = await Client.GetAsync("/some/spa/route");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task HealthReady_IsUnaffected()
    {
        var response = await Client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WrongVerbOnARealRoute_Returns405NotShadowedBy404()
    {
        var response = await Client.DeleteAsync("/api/v1/events");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
