namespace Soulsjwa.Api.Features.Auth.Entities;

/// <summary>
/// Coarse-grained role for a <see cref="User"/>. Admins can create and manage
/// any event, manage the allowlist, and manage competitors; regular users can
/// only manage events they create and participate as competitors / moderators.
/// </summary>
public enum UserRole
{
    User = 0,
    Admin = 1,
}
