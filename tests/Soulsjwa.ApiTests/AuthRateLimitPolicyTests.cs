using System.Net;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The "auth" rate-limit policy must partition per client IP rather than
/// sharing one process-wide bucket. Uses X-Forwarded-For (honoured because
/// ForwardLimit = 1 trusts exactly the immediate hop) to simulate distinct clients.
/// </summary>
public class AuthRateLimitPolicyTests : ApiTestBase
{
    [Fact]
    public async Task Revoke_OneIpExhaustingItsBucket_DoesNotBlockAnotherIp()
    {
        const string exhaustedIp = "203.0.113.1";
        const string otherIp = "203.0.113.2";

        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
        {
            last = await SendRevokeAsync(exhaustedIp);
        }

        last!.StatusCode.Should().Be((HttpStatusCode)429, "the 21st request from one IP within the window must be throttled");

        var otherResponse = await SendRevokeAsync(otherIp);
        otherResponse.StatusCode.Should().NotBe((HttpStatusCode)429,
            "a different client IP must not share the exhausted IP's bucket");
    }

    [Fact]
    public async Task Revoke_ForgedTwoHopForwardedFor_SharesBucketWithImmediatePeer()
    {
        // ForwardLimit = 1 means only the right-most (nearest-hop) address is trusted.
        // A client claiming an extra hop must not be able to spread its requests
        // across multiple partitions by varying the spoofed left-most address.
        const string immediatePeer = "203.0.113.9";

        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
        {
            last = await SendRevokeAsync($"10.0.0.{i % 250}, {immediatePeer}");
        }

        last!.StatusCode.Should().Be((HttpStatusCode)429,
            "requests must be partitioned by the trusted immediate-hop address, not the spoofable left-most entry");
    }

    private async Task<HttpResponseMessage> SendRevokeAsync(string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/revoke");
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await Client.SendAsync(request);
    }
}
