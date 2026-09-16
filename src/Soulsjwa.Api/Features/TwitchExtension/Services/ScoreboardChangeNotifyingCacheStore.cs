using Microsoft.AspNetCore.OutputCaching;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.TwitchExtension.Services;

/// <summary>
/// Wraps the output-cache store so that evicting an event's scoreboard tag —
/// the one thing every scoreboard write path already does, from completions
/// to live toggles to game switches — also tells the Twitch push notifier the
/// board changed. One hook instead of thirty call sites, and a write path
/// added later cannot forget it as long as it evicts the cache, which it must.
/// </summary>
public sealed class ScoreboardChangeNotifyingCacheStore(
    IOutputCacheStore inner,
    ITwitchExtensionPushNotifier notifier) : IOutputCacheStore
{
    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        if (CacheTags.TryParseScoreboard(tag, out var eventId))
            notifier.ScoreboardChanged(eventId);

        return inner.EvictByTagAsync(tag, cancellationToken);
    }

    public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
        inner.GetAsync(key, cancellationToken);

    public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken) =>
        inner.SetAsync(key, value, tags, validFor, cancellationToken);
}
