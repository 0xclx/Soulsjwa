namespace Soulsjwa.Api.Features.Auth.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TwitchId { get; set; } = string.Empty;
    public string TwitchLogin { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
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

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
}
