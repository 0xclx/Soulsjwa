using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Soulsjwa.Api.Diagnostics;

namespace Soulsjwa.Api.Features.TwitchExtension.Services;

/// <summary>
/// Pushes "something changed" pings to running extension front ends through
/// Twitch's Extension PubSub, so a completion shows up in every viewer's panel
/// about a second after it lands instead of on the next poll.
///
/// Two message kinds, both tiny (the 5 KB PubSub limit is far away):
/// <list type="bullet">
/// <item>A scoreboard change goes to the <c>global</c> target — every channel
/// the extension is active on — carrying the event id, and each panel refetches
/// only if that is the event it shows. Global rather than per channel because
/// most channels have no settings row (they follow the featured event) and so
/// cannot be enumerated.</item>
/// <item>A settings change goes to that one channel's <c>broadcast</c> target.</item>
/// </list>
/// Requests are queued and coalesced over a short window, since one connector
/// tick can evict the same board several times. Every failure is logged and
/// dropped: viewers still poll, so a lost push costs seconds, never data.
/// </summary>
public sealed class TwitchExtensionPushNotifier : BackgroundService, ITwitchExtensionPushNotifier
{
    /// <summary>The named <see cref="HttpClient"/> registered for this service in Program.cs.</summary>
    public const string HttpClientName = "TwitchExtensionPush";

    /// <summary>Twitch's Send Extension PubSub Message endpoint.</summary>
    public const string PubSubEndpoint = "https://api.twitch.tv/helix/extensions/pubsub";

    /// <summary>How long a queued push waits for more, so a burst of evictions becomes one message.</summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(2);

    /// <summary>The server-signed token only has to outlive one request.</summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);

    /// <summary>Enough for any realistic burst; beyond that the oldest pings are dropped, which a poll covers anyway.</summary>
    private const int QueueCapacity = 1024;

    /// <summary>The wire shape of <c>message</c>: the same for both kinds, so the front end has one parser.</summary>
    public static class MessageKinds
    {
        public const string Scoreboard = "scoreboard";
        public const string Configuration = "configuration";
    }

    /// <summary>What lands in the front end's <c>listen</c> callback. Serialised as camelCase, matching the API's own JSON.</summary>
    public sealed record PushMessage(string Type, Guid? EventId = null, string? ChannelId = null);

    private sealed record PushRequest(string Kind, string Target, PushMessage Message);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Channel<PushRequest> _queue = Channel.CreateBounded<PushRequest>(new BoundedChannelOptions(QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
    });

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TwitchExtensionOptions _options;
    private readonly ILogger<TwitchExtensionPushNotifier> _logger;
    private readonly TimeSpan _debounce;

    public TwitchExtensionPushNotifier(
        IHttpClientFactory httpClientFactory,
        TwitchExtensionOptions options,
        ILogger<TwitchExtensionPushNotifier> logger)
        : this(httpClientFactory, options, logger, DefaultDebounce)
    {
    }

    /// <summary>The debounce is injectable so tests need not wait two real seconds per push.</summary>
    internal TwitchExtensionPushNotifier(
        IHttpClientFactory httpClientFactory,
        TwitchExtensionOptions options,
        ILogger<TwitchExtensionPushNotifier> logger,
        TimeSpan debounce)
    {
        if (!options.CanPush)
            throw new InvalidOperationException(
                $"{nameof(TwitchExtensionPushNotifier)} needs {TwitchExtensionOptions.OwnerUserIdKey} to sign tokens; register {nameof(NullTwitchExtensionPushNotifier)} instead.");

        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
        _debounce = debounce;
    }

    public void ScoreboardChanged(Guid eventId) =>
        _queue.Writer.TryWrite(new PushRequest(
            MessageKinds.Scoreboard,
            TwitchExtensionAuth.PubSubTargets.Global,
            new PushMessage(MessageKinds.Scoreboard, EventId: eventId)));

    public void ConfigurationChanged(string channelId) =>
        _queue.Writer.TryWrite(new PushRequest(
            MessageKinds.Configuration,
            channelId,
            new PushMessage(MessageKinds.Configuration, ChannelId: channelId)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _queue.Reader;
        while (await reader.WaitToReadAsync(stoppingToken))
        {
            // Let the burst finish, then send each distinct ping once.
            await Task.Delay(_debounce, stoppingToken);

            var pending = new Dictionary<(string Kind, string Target, Guid? EventId), PushRequest>();
            while (reader.TryRead(out var request))
                pending[(request.Kind, request.Target, request.Message.EventId)] = request;

            foreach (var request in pending.Values)
                await SendAsync(request, stoppingToken);
        }
    }

    private async Task SendAsync(PushRequest request, CancellationToken ct)
    {
        var isGlobal = request.Target == TwitchExtensionAuth.PubSubTargets.Global;
        var token = TwitchExtensionAuth.CreateToken(
            _options.SigningKeys[0],
            isGlobal ? TwitchExtensionAuth.GlobalChannelId : request.Target,
            TwitchExtensionAuth.Roles.External,
            DateTime.UtcNow.Add(TokenLifetime),
            userId: _options.OwnerUserId,
            pubSubPerms: new Dictionary<string, string[]>
            {
                ["send"] = [isGlobal ? TwitchExtensionAuth.PubSubTargets.Global : TwitchExtensionAuth.PubSubTargets.Broadcast],
            });

        var body = new PubSubRequest(
            isGlobal ? [TwitchExtensionAuth.PubSubTargets.Global] : [TwitchExtensionAuth.PubSubTargets.Broadcast],
            isGlobal ? null : request.Target,
            isGlobal,
            JsonSerializer.Serialize(request.Message, JsonOptions));

        try
        {
            using var http = new HttpRequestMessage(HttpMethod.Post, PubSubEndpoint);
            http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            http.Headers.Add("Client-Id", _options.ClientId);
            http.Content = JsonContent.Create(body, options: JsonOptions);

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(http, ct);
            if (response.IsSuccessStatusCode)
                _logger.TwitchExtensionPushSent(request.Kind, request.Target);
            else
                _logger.TwitchExtensionPushRejected(request.Kind, request.Target, response.StatusCode);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.TwitchExtensionPushFailed(exception, request.Kind, request.Target);
        }
    }

    /// <summary>The request body Twitch's endpoint takes; field names are Twitch's.</summary>
    private sealed record PubSubRequest(
        [property: JsonPropertyName("target")] string[] Target,
        [property: JsonPropertyName("broadcaster_id")] string? BroadcasterId,
        [property: JsonPropertyName("is_global_broadcast")] bool IsGlobalBroadcast,
        [property: JsonPropertyName("message")] string Message);
}
