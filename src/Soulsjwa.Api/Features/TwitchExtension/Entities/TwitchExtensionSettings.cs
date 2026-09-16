using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.TwitchExtension.Entities;

/// <summary>
/// Extension-wide rules an admin sets for every channel that installs the
/// Twitch extension (<c>/admin/twitch-extension</c>). A singleton like
/// <c>SiteTheme</c>, but created lazily like <c>EventRules</c>: with no row
/// the defaults below apply. The extension's credentials are not here — they
/// stay in configuration (<c>TwitchExtension:*</c>), like every other secret.
/// </summary>
public class TwitchExtensionSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// Whether a broadcaster may show an event other than the featured one.
    /// Off, every channel follows the featured event whatever it saved, and
    /// the config views say so instead of offering a picker.
    /// </summary>
    public bool AllowChannelEventChoice { get; set; } = true;

    /// <summary>
    /// Whether viewers may switch between all games, the active game and any
    /// game. Off, the panel stays on the channel's default scope.
    /// </summary>
    public bool AllowViewerScopeSwitch { get; set; } = true;

    /// <summary>Scope a channel opens on until its broadcaster picks one; never <see cref="TwitchExtensionScope.PinnedGame"/>, which needs an event.</summary>
    public TwitchExtensionScope DefaultScope { get; set; } = TwitchExtensionScope.AllGames;

    /// <summary>Default for channels without their own settings.</summary>
    public bool DefaultHighlightChannelCompetitor { get; set; } = true;

    /// <summary>Default for channels without their own settings.</summary>
    public bool DefaultShowTrialProgress { get; set; } = true;

    public Guid? UpdatedById { get; set; }
    public User? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The rules that apply while no admin has saved any.</summary>
    public static TwitchExtensionSettings Defaults() => new();
}
