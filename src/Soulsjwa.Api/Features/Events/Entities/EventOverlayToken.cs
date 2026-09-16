using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

/// <summary>
/// An opaque token letting an unauthenticated client (a streamer's OBS browser
/// source) poll one event's scoreboard without an X-Api-Key. Scoped to a single
/// event and to the user who minted it — the owner/admin, or any competitor in
/// the event. Stored hashed, with only the prefix in cleartext for lookup and
/// display; the raw token is returned to the caller exactly once at creation.
/// </summary>
public class EventOverlayToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    /// <summary>Human-friendly label, e.g. "OBS – main scene".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 of the raw token, base64-encoded.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>First few characters of the random part of the token; safe to display in the UI.</summary>
    public string TokenPrefix { get; set; } = string.Empty;

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Null means the token never expires. Every creation path sets
    /// <c>OverlayTokensEndpoint.DefaultExpiryDays</c> from creation, so a
    /// null here can only come from a hand-edited row; it is honoured, not
    /// treated as an error.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// The saved look of this token's OBS source as JSON (see
    /// <c>OverlayTokenSettings</c>), or null for a token whose URL alone
    /// drives the overlay. Read on every overlay poll so a change reaches a
    /// source already on screen without its URL being re-pasted.
    /// </summary>
    public string? SettingsJson { get; set; }
}
