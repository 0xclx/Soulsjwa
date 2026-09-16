using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.TwitchExtension.Services;

/// <summary>Where the event a channel shows came from. Exposed on the wire by name so the panel can say "following the featured event".</summary>
public enum TwitchExtensionEventSource
{
    /// <summary>Nothing to show: no explicit event that still exists, and nothing featured.</summary>
    None = 0,

    /// <summary>The site's featured event — the default, and the fallback when an explicit pick is archived.</summary>
    Featured = 1,

    /// <summary>The event the broadcaster picked.</summary>
    Explicit = 2,
}

/// <param name="Policy">The extension-wide rules in force (defaults when no admin saved any).</param>
public sealed record ResolvedTwitchChannel(
    TwitchExtensionSettings Policy,
    TwitchExtensionChannelSettings? Settings,
    Event? Event,
    TwitchExtensionEventSource Source);

/// <summary>
/// The one rule for "which event does this channel show": the broadcaster's
/// explicit pick while the admin allows picks and it exists and is not
/// archived, otherwise the featured event, otherwise nothing. Shared by the
/// viewer read, the configuration views and the push notifier so they can
/// never disagree.
/// </summary>
public static class TwitchExtensionChannelResolver
{
    /// <summary>The extension-wide rules, or the defaults while no admin has saved any.</summary>
    public static async Task<TwitchExtensionSettings> LoadPolicyAsync(AppDbContext db, CancellationToken ct) =>
        await db.TwitchExtensionSettings.AsNoTracking().FirstOrDefaultAsync(ct)
        ?? TwitchExtensionSettings.Defaults();

    public static async Task<ResolvedTwitchChannel> ResolveAsync(string channelId, AppDbContext db, CancellationToken ct)
    {
        var policy = await LoadPolicyAsync(db, ct);
        var settings = await db.TwitchExtensionChannelSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChannelId == channelId, ct);

        if (policy.AllowChannelEventChoice && settings?.EventId is { } explicitId)
        {
            // The IsArchived query filter applies, so an archived pick reads
            // as absent and the channel falls back to the featured event
            // rather than showing a board nobody can score on any more.
            var explicitEvent = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == explicitId, ct);
            if (explicitEvent is not null)
                return new ResolvedTwitchChannel(policy, settings, explicitEvent, TwitchExtensionEventSource.Explicit);
        }

        var featured = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.IsFeatured, ct);
        return new ResolvedTwitchChannel(
            policy,
            settings,
            featured,
            featured is null ? TwitchExtensionEventSource.None : TwitchExtensionEventSource.Featured);
    }
}
