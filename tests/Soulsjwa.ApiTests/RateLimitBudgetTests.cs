using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The global fallback limiter (100/min per authenticated user) must not fight
/// an endpoint's own named policy. Before this fix, the connector submit
/// endpoint's 120/min policy could never be reached because the global bucket
/// exhausted first at 100.
/// </summary>
public class RateLimitBudgetTests : ApiTestBase
{
    [Fact]
    public async Task ConnectorSubmit_SustainsAboveTheGlobalPerUserLimit_WithoutBeingThrottled()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        // 105 requests exceeds the global fallback's 100/min-per-user budget
        // but stays under the connector policy's own 120/min — proving the
        // global limiter is not applied on top of the named policy.
        var statusCodes = new List<HttpStatusCode>();
        for (var i = 0; i < 105; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/connector/events/{Guid.NewGuid()}/games/{Guid.NewGuid()}/submit",
                new { data = "{}" });
            statusCodes.Add(response.StatusCode);
        }

        statusCodes.Should().NotContain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Revoke_WhenRateLimited_ResponseCarriesRetryAfterHeader()
    {
        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/revoke");
            request.Headers.Add("X-Forwarded-For", "203.0.113.77");
            last = await Client.SendAsync(request);
        }

        last!.StatusCode.Should().Be((HttpStatusCode)429);
        last.Headers.RetryAfter.Should().NotBeNull();
    }
}
