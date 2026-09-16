using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Soulsjwa.Connector.Services;
using Soulsjwa.Shared;

namespace Soulsjwa.ConnectorTests;

public class ApiServiceTests
{
    [Fact]
    public async Task GetEvents_BeforeConfigure_ShouldReturnFailure()
    {
        using var sut = new ApiService();

        var result = await sut.GetEventsAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not configured");
    }

    [Fact]
    public async Task Configure_ShouldSendApiKeyHeaderAndReturnPayload()
    {
        HttpRequestMessage? captured = null;
        var eventId = Guid.NewGuid();
        var payload = new List<ConnectorEventResponse>
        {
            new(eventId, "Summer race", "desc", true, false,
            [
                new ConnectorEventGameResponse(Guid.NewGuid(), GameIds.EldenRingMemory, "Elden Ring", "Elden Ring", true, ConnectorConstants.Version, true),
            ]),
        };

        using var sut = CreateService(req =>
        {
            captured = req;
            return JsonResponse(HttpStatusCode.OK, payload);
        });

        var result = await sut.GetEventsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.Id.Should().Be(eventId);
        result.Data![0].Games.Should().ContainSingle().Which.KnownGameId.Should().Be(GameIds.EldenRingMemory);
        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/api/v1/connector/events");
        captured.Headers.GetValues("X-Api-Key").Should().ContainSingle().Which.Should().Be("test-key");
    }

    /// <summary>
    /// The wire format as the API emits it (System.Text.Json web defaults:
    /// camelCase names), independent of the shared record type — so a change
    /// to serializer options on either side fails here rather than in a user's
    /// hands.
    /// </summary>
    [Fact]
    public async Task GetEvents_ShouldDeserializeTheApisCamelCaseWireFormat()
    {
        const string wire = """
            [{"id":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","name":"Evt","description":"","isStarted":false,"allowTrialRuns":true,
              "games":[{"eventGameId":"7c9e6679-7425-40de-944b-e07fc1f90ae7","knownGameId":9,"gameName":"Elden Ring","knownGameName":"Elden Ring",
                        "connectorSupported":true,"requiredConnectorVersion":"3.3.0","isEnabled":false}]}]
            """;
        using var sut = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(wire, Encoding.UTF8, "application/json"),
        });

        var result = await sut.GetEventsAsync();

        result.IsSuccess.Should().BeTrue();
        var ev = result.Data.Should().ContainSingle().Subject;
        ev.IsStarted.Should().BeFalse();
        ev.AllowTrialRuns.Should().BeTrue();
        var game = ev.Games.Should().ContainSingle().Subject;
        game.EventGameId.Should().Be(Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"));
        game.KnownGameId.Should().Be(9);
        game.ConnectorSupported.Should().BeTrue();
        game.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Configure_ShouldTrimTrailingSlashFromBaseUrl()
    {
        HttpRequestMessage? captured = null;
        using var sut = CreateService(
            req =>
            {
                captured = req;
                return JsonResponse(HttpStatusCode.OK, EmptyPayload());
            },
            baseUrl: "https://server.test/");

        _ = await sut.GetEventsAsync();

        captured!.RequestUri!.ToString().Should().Be("https://server.test/api/v1/connector/events");
    }


    [Theory]
    [InlineData("http://server.test")]
    [InlineData("ftp://server.test")]
    public void Configure_ShouldRejectNonHttpsNonLoopbackServerUrls(string serverUrl)
    {
        using var sut = new ApiService();

        var act = () => sut.Configure(serverUrl, "test-key");

        act.Should().Throw<UriFormatException>()
            .WithMessage("*HTTPS*");
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1:5000")]
    [InlineData("http://[::1]:5000")]
    public void Configure_ShouldAllowHttpLoopbackForLocalDevelopment(string serverUrl)
    {
        using var sut = new ApiService();

        var act = () => sut.Configure(serverUrl, "test-key");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetEvents_OnUnauthorized_ShouldReturnApiKeyFailure()
    {
        using var sut = CreateService(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await sut.GetEventsAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API key");
    }

    [Fact]
    public async Task GetEvents_OnForbidden_ShouldReturnAccessFailure()
    {
        using var sut = CreateService(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await sut.GetEventsAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Forbidden");
    }

    [Fact]
    public async Task GetEvents_OnEmptyBody_ShouldReturnFailure()
    {
        using var sut = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        });

        var result = await sut.GetEventsAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task GetEvents_OnNetworkError_ShouldReturnConnectionFailure()
    {
        using var sut = CreateService(_ => throw new HttpRequestException("DNS failure"));

        var result = await sut.GetEventsAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection error");
    }

    [Fact]
    public async Task GetEvents_OnTimeout_ShouldReturnTimeoutFailure()
    {
        using var sut = CreateService(_ => throw new TaskCanceledException());

        var result = await sut.GetEventsAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task GetEvents_OnUnexpectedStatus_ShouldReturnFailure()
    {
        using var sut = CreateService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sut.GetEventsAsync();

        // EnsureSuccessStatusCode throws HttpRequestException -> mapped to "Connection error".
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ApiService owns its HttpClient (created inside Configure), so we use
    // reflection to swap in one backed by a stub handler after configuring.
    // The X-Api-Key header set by Configure is preserved on the replacement
    // so we can still assert that it was attached to outgoing requests.
    private static ApiService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        string baseUrl = "https://server.test",
        string apiKey = "test-key")
    {
        var service = new ApiService();
        service.Configure(baseUrl, apiKey);

        var stub = new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/')),
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
            stub.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var field = typeof(ApiService).GetField(
            "_httpClient",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        (field.GetValue(service) as IDisposable)?.Dispose();
        field.SetValue(service, stub);

        return service;
    }

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode status, T payload) =>
        new(status) { Content = JsonContent.Create(payload) };

    private static List<ConnectorEventResponse> EmptyPayload() => [];

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(responder(request));
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}
