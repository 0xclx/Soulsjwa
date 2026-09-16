using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

public class TwitchAuthServiceTests : IntegrationTestBase
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Twitch:ClientId"] = "client-id",
            ["Twitch:ClientSecret"] = "client-secret",
            ["Twitch:RedirectUri"] = "http://localhost/cb",
        })
        .Build();

    [Fact]
    public void GetAuthorizationUrl_BuildsCorrectQueryParams()
    {
        var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException("not expected")));
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var url = svc.GetAuthorizationUrl("state-xyz");

        url.Should().StartWith("https://id.twitch.tv/oauth2/authorize?");
        url.Should().Contain("client_id=client-id");
        url.Should().Contain("redirect_uri=http%3A%2F%2Flocalhost%2Fcb");
        url.Should().Contain("response_type=code");
        url.Should().Contain("scope=user%3Aread%3Aemail");
        url.Should().Contain("state=state-xyz");
    }

    [Fact]
    public async Task ExchangeCodeAsync_HappyPath_ReturnsParsedResponse()
    {
        var json = """{"access_token":"acc","refresh_token":"ref","expires_in":3600,"token_type":"bearer"}""";
        var http = new HttpClient(new StubHandler((req, _) =>
        {
            req.RequestUri!.ToString().Should().Be("https://id.twitch.tv/oauth2/token");
            return Task.FromResult(JsonResponse(json));
        }));

        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);
        var result = await svc.ExchangeCodeAsync("the-code");

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("acc");
        result.RefreshToken.Should().Be("ref");
        result.ExpiresIn.Should().Be(3600);
        result.TokenType.Should().Be("bearer");
    }

    [Fact]
    public async Task ExchangeCodeAsync_NonSuccessStatus_ReturnsNull()
    {
        var http = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var result = await svc.ExchangeCodeAsync("bad");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserInfoAsync_HappyPath_ReturnsFirstUser()
    {
        var json = """
        {"data":[{"id":"42","login":"alice","display_name":"Alice","email":"a@x.com","profile_image_url":"http://img"}]}
        """;
        var http = new HttpClient(new StubHandler((req, _) =>
        {
            req.RequestUri!.ToString().Should().Be("https://api.twitch.tv/helix/users");
            req.Headers.GetValues("Client-Id").Single().Should().Be("client-id");
            return Task.FromResult(JsonResponse(json));
        }));

        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);
        var info = await svc.GetUserInfoAsync("the-token");

        info.Should().NotBeNull();
        info!.Id.Should().Be("42");
        info.Login.Should().Be("alice");
        info.DisplayName.Should().Be("Alice");
        info.Email.Should().Be("a@x.com");
        info.ProfileImageUrl.Should().Be("http://img");
    }

    [Fact]
    public async Task GetUserInfoAsync_EmptyData_ReturnsNull()
    {
        var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(JsonResponse("""{"data":[]}"""))));
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var info = await svc.GetUserInfoAsync("the-token");
        info.Should().BeNull();
    }

    [Fact]
    public async Task GetUserInfoAsync_BodyMissingDataProperty_ReturnsNull()
    {
        // A 200 with an unexpected shape must degrade to null, not throw —
        // GetProperty("data") used to throw KeyNotFoundException here.
        var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(JsonResponse("""{"unexpected":true}"""))));
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var info = await svc.GetUserInfoAsync("the-token");
        info.Should().BeNull();
    }

    [Fact]
    public async Task GetUserInfoAsync_MalformedJsonBody_ReturnsNull()
    {
        var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(JsonResponse("not json at all"))));
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var info = await svc.GetUserInfoAsync("the-token");
        info.Should().BeNull();
    }

    [Fact]
    public async Task GetUserInfoAsync_OversizedBody_ReturnsNullInsteadOfBufferingIt()
    {
        var oversized = new string('a', 100_000);
        var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(JsonResponse($$"""{"data":[{"id":"1","login":"{{oversized}}","display_name":"x"}]}"""))));
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var info = await svc.GetUserInfoAsync("the-token");
        info.Should().BeNull();
    }

    [Fact]
    public async Task GetUserInfoAsync_NonSuccess_ReturnsNull()
    {
        var http = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var info = await svc.GetUserInfoAsync("the-token");
        info.Should().BeNull();
    }

    [Fact]
    public async Task UpsertUserAsync_NewUser_CreatesAndPersists()
    {
        var db = CreateDbContext();
        db.AllowlistedTwitchLogins.Add(new Soulsjwa.Api.Features.Auth.Entities.AllowlistedTwitchLogin { TwitchLogin = "newbie" });
        await db.SaveChangesAsync();
        var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException()));
        var svc = new TwitchAuthService(http, Config, db, NullLogger<TwitchAuthService>.Instance);

        var u = await svc.UpsertUserAsync(new TwitchUserInfo("99", "newbie", "Newbie", "n@x.com", null));

        u.Id.Should().NotBe(Guid.Empty);
        u.TwitchId.Should().Be("99");
        u.TwitchLogin.Should().Be("newbie");
        u.IsAllowlisted.Should().BeTrue();
        db.Users.Should().ContainSingle(x => x.TwitchId == "99");
    }

    [Fact]
    public async Task UpsertUserAsync_ExistingUser_UpdatesFieldsInPlace()
    {
        var db = CreateDbContext();
        db.AllowlistedTwitchLogins.Add(new Soulsjwa.Api.Features.Auth.Entities.AllowlistedTwitchLogin { TwitchLogin = "old" });
        db.AllowlistedTwitchLogins.Add(new Soulsjwa.Api.Features.Auth.Entities.AllowlistedTwitchLogin { TwitchLogin = "new" });
        await db.SaveChangesAsync();
        var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException()));
        var svc = new TwitchAuthService(http, Config, db, NullLogger<TwitchAuthService>.Instance);

        var first = await svc.UpsertUserAsync(new TwitchUserInfo("99", "old", "Old Name", null, null));
        var firstId = first.Id;
        await Task.Delay(10); // ensure UpdatedAt advances

        var second = await svc.UpsertUserAsync(new TwitchUserInfo("99", "new", "New Name", "n@x.com", "http://img"));

        second.Id.Should().Be(firstId, "upsert should match by TwitchId, not create a new row");
        second.TwitchLogin.Should().Be("new");
        second.DisplayName.Should().Be("New Name");
        second.Email.Should().Be("n@x.com");
        second.ProfileImageUrl.Should().Be("http://img");
        db.Users.Count().Should().Be(1);
    }

    [Fact]
    public async Task UpsertUserAsync_NotAllowlisted_Throws()
    {
        var db = CreateDbContext();
        var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException()));
        var svc = new TwitchAuthService(http, Config, db, NullLogger<TwitchAuthService>.Instance);

        var act = async () => await svc.UpsertUserAsync(new TwitchUserInfo("1", "stranger", "Stranger", null, null));

        await act.Should().ThrowAsync<NotAllowlistedException>();
        db.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertUserAsync_DeAllowlistedExistingUser_Throws()
    {
        var db = CreateDbContext();
        // user exists but admin has revoked their allowlist
        db.Users.Add(new Soulsjwa.Api.Features.Auth.Entities.User
        {
            TwitchId = "1",
            TwitchLogin = "ex",
            DisplayName = "Ex",
            IsAllowlisted = false,
        });
        await db.SaveChangesAsync();
        var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException()));
        var svc = new TwitchAuthService(http, Config, db, NullLogger<TwitchAuthService>.Instance);

        var act = async () => await svc.UpsertUserAsync(new TwitchUserInfo("1", "ex", "Ex", null, null));

        await act.Should().ThrowAsync<NotAllowlistedException>();
    }

    [Fact]
    public async Task UpsertUserAsync_BootstrapAdminLogin_PromotesToAdmin()
    {
        var db = CreateDbContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Twitch:ClientId"] = "c",
                ["Twitch:ClientSecret"] = "s",
                ["Twitch:RedirectUri"] = "http://x",
                ["Admin:BootstrapTwitchLogin"] = "FounderTV",
            }).Build();
        var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException()));
        var svc = new TwitchAuthService(http, config, db, NullLogger<TwitchAuthService>.Instance);

        var u = await svc.UpsertUserAsync(new TwitchUserInfo("42", "foundertv", "FounderTV", null, null));

        u.Role.Should().Be(Soulsjwa.Api.Features.Auth.Entities.UserRole.Admin);
        u.IsAllowlisted.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertUserAsync_BootstrapWhenAdminAlreadyExists_DoesNotPromote()
    {
        var db = CreateDbContext();
        db.Users.Add(new Soulsjwa.Api.Features.Auth.Entities.User
        {
            TwitchId = "0",
            TwitchLogin = "first",
            DisplayName = "First",
            Role = Soulsjwa.Api.Features.Auth.Entities.UserRole.Admin,
            IsAllowlisted = true,
        });
        db.AllowlistedTwitchLogins.Add(new Soulsjwa.Api.Features.Auth.Entities.AllowlistedTwitchLogin { TwitchLogin = "foundertv" });
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Twitch:ClientId"] = "c",
                ["Twitch:ClientSecret"] = "s",
                ["Twitch:RedirectUri"] = "http://x",
                ["Admin:BootstrapTwitchLogin"] = "FounderTV",
            }).Build();
        var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException()));
        var svc = new TwitchAuthService(http, config, db, NullLogger<TwitchAuthService>.Instance);

        var u = await svc.UpsertUserAsync(new TwitchUserInfo("42", "foundertv", "FounderTV", null, null));

        u.Role.Should().Be(Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
    }

    // These exercise the actual resilience pipeline registered in Program.cs
    // (via the shared TwitchClientOptions.ConfigureResilience), not a
    // re-implementation of it.

    [Fact]
    public async Task ExchangeCodeAsync_PersistentServerError_IsNeverRetried()
    {
        // The OAuth code is single-use: retrying the POST after a lost
        // response would replay it. One attempt only, regardless of MaxRetryAttempts.
        var attempts = 0;
        var http = CreateResilientClient((_, _) =>
        {
            Interlocked.Increment(ref attempts);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }, retryAttempts: 2, attemptTimeoutSeconds: 5, totalTimeoutSeconds: 20);
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var result = await svc.ExchangeCodeAsync("the-code");

        result.Should().BeNull();
        attempts.Should().Be(1, "the token-exchange POST must never be replayed");
    }

    [Fact]
    public async Task GetUserInfoAsync_TransientServerError_RetriesAndSucceeds()
    {
        var attempts = 0;
        var http = CreateResilientClient((_, _) =>
        {
            var attempt = Interlocked.Increment(ref attempts);
            return Task.FromResult(attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : JsonResponse("""{"data":[{"id":"1","login":"alice","display_name":"Alice"}]}"""));
        }, retryAttempts: 2, attemptTimeoutSeconds: 5, totalTimeoutSeconds: 20);
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var info = await svc.GetUserInfoAsync("the-token");

        info.Should().NotBeNull();
        info!.Login.Should().Be("alice");
        attempts.Should().Be(2, "the idempotent GET is retried once after a 5xx");
    }

    [Fact]
    public async Task GetUserInfoAsync_HangingResponse_FailsWithinConfiguredTimeoutNotTheHttpClientDefault()
    {
        var http = CreateResilientClient(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, retryAttempts: 1, attemptTimeoutSeconds: 1, totalTimeoutSeconds: 3);
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var act = async () => await svc.GetUserInfoAsync("the-token");
        await act.Should().ThrowAsync<Polly.Timeout.TimeoutRejectedException>();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "a hang must fail within the configured timeout, not HttpClient's 100s default");
    }

    [Fact]
    public async Task RepeatedFailures_OpenTheCircuitBreaker_SubsequentCallsFailFastWithoutAnOutboundCall()
    {
        var attempts = 0;
        var http = CreateResilientClient((_, _) =>
        {
            Interlocked.Increment(ref attempts);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }, retryAttempts: 1, attemptTimeoutSeconds: 5, totalTimeoutSeconds: 20);
        var svc = new TwitchAuthService(http, Config, CreateDbContext(), NullLogger<TwitchAuthService>.Instance);

        // MinimumThroughput is 4: fire enough failing calls to trip the breaker.
        // Each logical call makes up to 2 HTTP attempts (1 retry), so the
        // breaker may open partway through — a call started before it opened
        // can still surface as BrokenCircuitException once the retry runs.
        for (var i = 0; i < TwitchClientOptions.CircuitBreakerMinimumThroughput; i++)
        {
            try { await svc.GetUserInfoAsync("the-token"); }
            catch (BrokenCircuitException) { }
        }

        var attemptsBeforeOpen = attempts;
        var act = async () => await svc.GetUserInfoAsync("the-token");

        await act.Should().ThrowAsync<BrokenCircuitException>();
        attempts.Should().Be(attemptsBeforeOpen, "an open circuit must fail fast without an outbound call");
    }

    private static HttpClient CreateResilientClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> impl,
        int retryAttempts, int attemptTimeoutSeconds, int totalTimeoutSeconds)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("twitch-test")
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(impl))
            .AddStandardResilienceHandler(options =>
            {
                TwitchClientOptions.ConfigureResilience(options, retryAttempts, attemptTimeoutSeconds, totalTimeoutSeconds);
                // Keep the tests fast — production leaves this at Polly's default.
                options.Retry.Delay = TimeSpan.FromMilliseconds(1);
            });
        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("twitch-test");
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> impl)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => impl(request, cancellationToken);
    }
}
