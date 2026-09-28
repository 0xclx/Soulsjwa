namespace Soulsjwa.Api.Features.Auth.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TwitchId { get; set; } = string.Empty;
    public string TwitchLogin { get; set; } = string.Empty;
    /// <summary>
    /// The name every surface shows: <see cref="DisplayNameOverride"/> when set,
    /// otherwise <see cref="TwitchDisplayName"/>. Stored so the many readers
    /// and projections need no knowledge of the override; production code keeps
    /// it in sync through <see cref="SetTwitchDisplayName"/> and
    /// <see cref="SetDisplayNameOverride"/> only.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The display name Twitch last reported; refreshed on every sign-in.</summary>
    public string TwitchDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// A name the user (or an admin) chose. Takes precedence over Twitch's and
    /// survives sign-in; null means "use the Twitch name".
    /// </summary>
    public string? DisplayNameOverride { get; set; }
    public string? Email { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Coarse-grained role. Admins can manage every event, the allowlist and
    /// user roles.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// When false, the user is blocked from logging in via Twitch even if the
    /// row already exists. An admin must add their Twitch login to the
    /// allowlist before they can sign up or sign back in.
    /// </summary>
    public bool IsAllowlisted { get; set; }

    public const int DisplayNameOverrideMaxLength = 50;

    public void SetTwitchDisplayName(string twitchDisplayName)
    {
        TwitchDisplayName = twitchDisplayName;
        DisplayName = DisplayNameOverride ?? TwitchDisplayName;
    }

    public void SetDisplayNameOverride(string? displayNameOverride)
    {
        DisplayNameOverride = displayNameOverride;
        DisplayName = DisplayNameOverride ?? TwitchDisplayName;
    }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
}
