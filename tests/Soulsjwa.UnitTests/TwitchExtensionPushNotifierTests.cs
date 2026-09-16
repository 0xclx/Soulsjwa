using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Features.TwitchExtension;
using Soulsjwa.Api.Features.TwitchExtension.Services;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// The push path in memory: a scoreboard change becomes one global PubSub
/// message signed as the extension owner, a burst is coalesced, and a
/// settings change targets the channel. Twitch itself is a stub handler.
/// </summary>
public class TwitchExtensionPushNotifierTests
{
    private static readonly byte[] Secret = Enumerable.Repeat((byte)5, 32).ToArray();
    private const string OwnerUserId = "44322889";
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(50);

    private static readonly TwitchExtensionOptions Options = TwitchExtensionOptions.FromConfiguration(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [TwitchExtensionOptions.ClientIdKey] = "clientid",
            [TwitchExtensionOptions.SecretKey] = Convert.ToBase64String(Secret),
            [TwitchExtensionOptions.OwnerUserIdKey] = OwnerUserId,
        }).Build());

    private sealed record Sent(HttpRequestMessage Request, JsonDocument Body, string Token);

    private sealed class Twitch : IHttpClientFactory
    {
        private readonly List<Sent> _sent = [];
        private readonly SemaphoreSlim _arrived = new(0);
        public HttpStatusCode Reply { get; set; } = HttpStatusCode.NoContent;

        public HttpClient CreateClient(string name) => new(new Handler(this));

        public async Task<Sent> NextAsync()
        {
            (await _arrived.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue("a push should have been sent");
            lock (_sent) return _sent[^1];
        }

        public int Count { get { lock (_sent) return _sent.Count; } }

        private sealed class Handler(Twitch owner) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var json = await request.Content!.ReadAsStringAsync(ct);
                var token = request.Headers.Authorization!.Parameter!;
                lock (owner._sent) owner._sent.Add(new Sent(request, JsonDocument.Parse(json), token));
                owner._arrived.Release();
                return new HttpResponseMessage(owner.Reply);
            }
        }
    }

    private static async Task<(TwitchExtensionPushNotifier Notifier, Twitch Twitch)> StartAsync()
    {
        var twitch = new Twitch();
        var notifier = new TwitchExtensionPushNotifier(twitch, Options, NullLogger<TwitchExtensionPushNotifier>.Instance, Debounce);
        await notifier.StartAsync(CancellationToken.None);
        return (notifier, twitch);
    }

    [Fact]
    public async Task AScoreboardChange_IsOneGlobalMessage_SignedAsTheOwner()
    {
        var (notifier, twitch) = await StartAsync();
        var eventId = Guid.NewGuid();

        notifier.ScoreboardChanged(eventId);
        var sent = await twitch.NextAsync();
        await notifier.StopAsync(CancellationToken.None);

        sent.Request.RequestUri!.ToString().Should().Be(TwitchExtensionPushNotifier.PubSubEndpoint);
        sent.Request.Headers.GetValues("Client-Id").Should().Equal("clientid");
        var body = sent.Body.RootElement;
        body.GetProperty("is_global_broadcast").GetBoolean().Should().BeTrue();
        body.GetProperty("target").EnumerateArray().Select(t => t.GetString()).Should().Equal(TwitchExtensionAuth.PubSubTargets.Global);
        body.TryGetProperty("broadcaster_id", out _).Should().BeFalse();
        var message = JsonDocument.Parse(body.GetProperty("message").GetString()!).RootElement;
        message.GetProperty("type").GetString().Should().Be(TwitchExtensionPushNotifier.MessageKinds.Scoreboard);
        message.GetProperty("eventId").GetGuid().Should().Be(eventId);

        var claims = new JsonWebToken(sent.Token);
        claims.GetClaim(TwitchExtensionAuth.Claims.Role).Value.Should().Be(TwitchExtensionAuth.Roles.External);
        claims.GetClaim(TwitchExtensionAuth.Claims.UserId).Value.Should().Be(OwnerUserId);
        claims.GetClaim(TwitchExtensionAuth.Claims.ChannelId).Value.Should().Be(TwitchExtensionAuth.GlobalChannelId);
        claims.GetClaim(TwitchExtensionAuth.Claims.PubSubPerms).Value.Should().Contain(TwitchExtensionAuth.PubSubTargets.Global);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(sent.Token, TwitchExtensionAuth.BuildValidationParameters(Options));
        validation.IsValid.Should().BeTrue("Twitch verifies it with the same secret");
    }

    [Fact]
    public async Task ABurstOfChangesToOneEvent_IsCoalescedIntoOneMessage()
    {
        var (notifier, twitch) = await StartAsync();
        var eventId = Guid.NewGuid();

        for (var i = 0; i < 20; i++) notifier.ScoreboardChanged(eventId);
        await twitch.NextAsync();
        await Task.Delay(Debounce * 4);
        await notifier.StopAsync(CancellationToken.None);

        twitch.Count.Should().Be(1);
    }

    [Fact]
    public async Task AConfigurationChange_TargetsTheChannel()
    {
        var (notifier, twitch) = await StartAsync();

        notifier.ConfigurationChanged("777");
        var sent = await twitch.NextAsync();
        await notifier.StopAsync(CancellationToken.None);

        var body = sent.Body.RootElement;
        body.GetProperty("is_global_broadcast").GetBoolean().Should().BeFalse();
        body.GetProperty("broadcaster_id").GetString().Should().Be("777");
        body.GetProperty("target").EnumerateArray().Select(t => t.GetString()).Should().Equal(TwitchExtensionAuth.PubSubTargets.Broadcast);
        JsonDocument.Parse(body.GetProperty("message").GetString()!).RootElement.GetProperty("channelId").GetString().Should().Be("777");
        new JsonWebToken(sent.Token).GetClaim(TwitchExtensionAuth.Claims.ChannelId).Value.Should().Be("777");
    }

    [Fact]
    public async Task ARejectedPush_IsDroppedAndTheNextOneStillGoesOut()
    {
        var (notifier, twitch) = await StartAsync();
        twitch.Reply = HttpStatusCode.TooManyRequests;

        notifier.ScoreboardChanged(Guid.NewGuid());
        await twitch.NextAsync();
        twitch.Reply = HttpStatusCode.NoContent;
        notifier.ScoreboardChanged(Guid.NewGuid());
        await twitch.NextAsync();
        await notifier.StopAsync(CancellationToken.None);

        twitch.Count.Should().Be(2);
    }

    [Fact]
    public void WithoutAnOwnerUserId_TheNotifierRefusesToBeConstructed()
    {
        var options = TwitchExtensionOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [TwitchExtensionOptions.ClientIdKey] = "clientid",
            [TwitchExtensionOptions.SecretKey] = Convert.ToBase64String(Secret),
        }).Build());

        var act = () => new TwitchExtensionPushNotifier(new Twitch(), options, NullLogger<TwitchExtensionPushNotifier>.Instance);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{TwitchExtensionOptions.OwnerUserIdKey}*");
    }

    private sealed class RecordingNotifier : ITwitchExtensionPushNotifier
    {
        public List<Guid> Events { get; } = [];
        public void ScoreboardChanged(Guid eventId) => Events.Add(eventId);
        public void ConfigurationChanged(string channelId) { }
    }

    private sealed class NullStore : IOutputCacheStore
    {
        public List<string> Evicted { get; } = [];
        public ValueTask EvictByTagAsync(string tag, CancellationToken ct) { Evicted.Add(tag); return ValueTask.CompletedTask; }
        public ValueTask<byte[]?> GetAsync(string key, CancellationToken ct) => ValueTask.FromResult<byte[]?>(null);
        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken ct) => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task EvictingAScoreboardTag_NotifiesTheEvent_AndStillEvicts()
    {
        var inner = new NullStore();
        var notifier = new RecordingNotifier();
        var store = new ScoreboardChangeNotifyingCacheStore(inner, notifier);
        var eventId = Guid.NewGuid();

        await store.EvictByTagAsync(CacheTags.Scoreboard(eventId), CancellationToken.None);
        await store.EvictByTagAsync(CacheTags.Calendar, CancellationToken.None);

        notifier.Events.Should().Equal(eventId);
        inner.Evicted.Should().Equal(CacheTags.Scoreboard(eventId), CacheTags.Calendar);
    }

    [Fact]
    public async Task DecoratingTheOutputCacheStore_WrapsTheRegisteredStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOutputCache();
        var notifier = new RecordingNotifier();
        services.AddSingleton<ITwitchExtensionPushNotifier>(notifier);
        services.DecorateOutputCacheStoreForPush();
        await using var provider = services.BuildServiceProvider();
        var eventId = Guid.NewGuid();

        var store = provider.GetRequiredService<IOutputCacheStore>();
        await store.EvictByTagAsync(CacheTags.Scoreboard(eventId), CancellationToken.None);

        store.Should().BeOfType<ScoreboardChangeNotifyingCacheStore>();
        notifier.Events.Should().Equal(eventId);
    }

    [Theory]
    [InlineData("scoreboard:not-a-guid")]
    [InlineData("calendar")]
    [InlineData("twitch-extension:123")]
    public void TryParseScoreboard_RejectsOtherTags(string tag)
    {
        CacheTags.TryParseScoreboard(tag, out _).Should().BeFalse();
    }
}
