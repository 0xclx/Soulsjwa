using System.Text;

namespace Soulsjwa.Api.Infrastructure.Auth;

/// <summary>
/// Validates <c>Jwt:Secret</c> strength at startup. Static rather than inlined in
/// Program.cs so it is testable without booting a host.
/// </summary>
public static class JwtSecretValidator
{
    /// <summary>256-bit minimum key size for HMAC-SHA256.</summary>
    public const int MinJwtSecretBytes = 32;

    /// <summary>
    /// Must never sign tokens outside Development. Includes the value committed to
    /// appsettings.Development.json.
    /// </summary>
    private static readonly string[] KnownPlaceholderSecrets =
    [
        "dev-secret-change-in-production-32chars!!",
        "changeme",
        "secret",
    ];

    /// <summary>Returns <c>null</c> when the secret is acceptable, otherwise the startup error to fail with.</summary>
    public static string? Validate(string? secret, bool isDevelopment)
    {
        // IsNullOrWhiteSpace, not `is null` — a Compose variable substitution for an
        // unset $JWT_SECRET expands to "", which is non-null and must still fail.
        if (string.IsNullOrWhiteSpace(secret))
            return "Jwt:Secret must be configured. Set the Jwt__Secret environment variable.";

        if (Encoding.UTF8.GetByteCount(secret) < MinJwtSecretBytes)
            return $"Jwt:Secret must be at least {MinJwtSecretBytes} bytes. Generate one with: openssl rand -base64 48";

        if (!isDevelopment && KnownPlaceholderSecrets.Contains(secret, StringComparer.Ordinal))
            return "Jwt:Secret must not be a known placeholder value outside Development. Generate one with: openssl rand -base64 48";

        return null;
    }
}
