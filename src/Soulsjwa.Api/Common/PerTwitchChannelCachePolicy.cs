using Microsoft.AspNetCore.OutputCaching;
using Soulsjwa.Api.Features.TwitchExtension;

namespace Soulsjwa.Api.Common;

/// <summary>
/// Output-cache policy for the Twitch extension's viewer reads. Every viewer
/// of a channel asks for the same board, so the response is cached per
/// channel — keyed on the <c>channel_id</c> claim of the verified token, not on
/// the token itself, which differs per viewer and would make the cache useless.
///
/// The default policy refuses to cache any request carrying an
/// <c>Authorization</c> header; this one turns caching back on. That is safe
/// only because <c>UseAuthentication</c>/<c>UseAuthorization</c> run before
/// <c>UseOutputCache</c> in Program.cs, so a cached body is served only after
/// the caller's token for that channel was verified. The entry is tagged with
/// the channel (evicted when the broadcaster saves settings) and, once the
/// handler has resolved which event the channel shows, with that event's
/// <see cref="CacheTags.Scoreboard"/> tag — so every completion, live toggle or
/// game switch evicts it exactly like the in-app scoreboard and the overlay.
/// </summary>
public sealed class PerTwitchChannelCachePolicy : IOutputCachePolicy
{
    public const string PolicyName = "TwitchExtensionScoreboard";

    /// <summary>
    /// <c>HttpContext.Items</c> key under which the handler leaves the resolved
    /// event id, read back here when the response is stored.
    /// </summary>
    public const string ResolvedEventItemKey = "TwitchExtension.ResolvedEventId";

    private const string ChannelVaryKey = "twitch-channel";

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var channelId = TwitchExtensionAuth.GetChannelId(context.HttpContext.User);
        if (channelId is null)
        {
            // Unauthenticated requests never reach the handler (authorization
            // rejects them first); this only guards against a policy applied
            // to a route without the scheme.
            context.EnableOutputCaching = false;
            return ValueTask.CompletedTask;
        }

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;
        context.AllowLocking = true;
        context.CacheVaryByRules.VaryByValues[ChannelVaryKey] = channelId;
        context.Tags.Add(CacheTags.TwitchExtensionChannel(channelId));
        context.Tags.Add(CacheTags.TwitchExtensionAll);

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        if (context.HttpContext.Items.TryGetValue(ResolvedEventItemKey, out var raw) && raw is Guid eventId)
            context.Tags.Add(CacheTags.Scoreboard(eventId));

        return ValueTask.CompletedTask;
    }
}
