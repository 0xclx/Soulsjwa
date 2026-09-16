namespace Soulsjwa.Api.Features.Auth.Entities;

/// <summary>
/// An entry in the admin-managed Twitch login allowlist. Only users whose
/// Twitch login (case-insensitive) appears in this table are permitted to
/// complete the Twitch OAuth flow and create / use an account.
/// </summary>
public class AllowlistedTwitchLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The Twitch login (handle), stored lowercase for case-insensitive lookups.</summary>
    public string TwitchLogin { get; set; } = string.Empty;

    /// <summary>Free-form note explaining why this login was added.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// The admin who added the entry. Nullable (and set to null on that
    /// admin's deletion) so an entry outlives the account that created it;
    /// nothing creates entries without an actor — the bootstrap admin is
    /// marked allowlisted on the user row, not here.
    /// </summary>
    public Guid? AddedById { get; set; }
    public User? AddedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
