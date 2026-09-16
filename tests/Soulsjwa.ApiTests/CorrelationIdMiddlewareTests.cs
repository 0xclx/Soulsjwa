using System.Net;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The inbound X-Correlation-Id header is untrusted client input. Serilog's
/// Console sink writes to Console.Out, so redirecting it captures exactly what
/// a real log aggregator would receive — same technique as
/// OverlayTokenLoggingTests.
/// </summary>
[Collection(ConsoleCaptureCollection.Name)]
public class CorrelationIdMiddlewareTests : ApiTestBase
{
    [Fact]
    public async Task ValidCorrelationId_IsEchoedBackUnchanged()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.Add("X-Correlation-Id", "abc-123");

        var response = await Client.SendAsync(request);

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("abc-123");
    }

    [Fact]
    public async Task OversizedCorrelationId_IsReplacedAndNeverLogged()
    {
        var oversized = new string('a', 10 * 1024);
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.Add("X-Correlation-Id", oversized);

        var originalOut = Console.Out;
        var capture = new ConsoleCapture();
        Console.SetOut(capture);
        string echoed;
        try
        {
            var response = await Client.SendAsync(request);
            response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
            echoed = values!.Single();
            // The request-logging middleware flushes asynchronously, so wait
            // for a line to arrive rather than guessing at a duration. That
            // also keeps the negative assertion below honest: it would pass
            // trivially against an empty capture.
            await WaitForCapturedOutputAsync(capture);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        echoed.Should().NotBe(oversized);
        capture.ToString().Should().NotContain(oversized, "the oversized supplied value must never reach application logs");
    }

    [Fact]
    public async Task CorrelationIdWithEmbeddedCrlf_IsReplacedAndNotInjected()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "a\r\nInjected: 1");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Injected").Should().BeFalse();
        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().NotContain("Injected");
    }

    [Fact]
    public async Task NoCorrelationIdHeader_StillGetsAGeneratedValue()
    {
        var response = await Client.GetAsync("/health/ready");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }
}
