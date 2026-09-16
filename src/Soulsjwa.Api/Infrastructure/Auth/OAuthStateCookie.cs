using Microsoft.AspNetCore.DataProtection;

namespace Soulsjwa.Api.Infrastructure.Auth;

/// <summary>
/// Holds the OAuth2 <c>state</c> in a signed+encrypted cookie rather than server-side
/// session, so any node behind a load balancer can validate the callback without
/// sticky sessions or a shared cache.
/// </summary>
public sealed class OAuthStateCookie(IDataProtectionProvider dataProtectionProvider)
{
    public const string Name = "oauth_state";
    private const string Purpose = "Soulsjwa.OAuthState.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    public void Issue(HttpResponse response, string state)
    {
        var payload = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:{state}";
        var protectedValue = _protector.Protect(payload);

        response.Cookies.Append(Name, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax, // OAuth callback is a cross-site top-level GET → Lax (not Strict)
            IsEssential = true,
            Path = "/api/v1/auth",
            Expires = DateTimeOffset.UtcNow.AddMinutes(10),
        });
    }

    public bool TryConsume(HttpContext context, string presentedState)
    {
        if (!context.Request.Cookies.TryGetValue(Name, out var protectedValue) || string.IsNullOrEmpty(protectedValue))
            return false;

        // Always clear the cookie on attempt — single-use.
        context.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/api/v1/auth",
        });

        string payload;
        try
        {
            payload = _protector.Unprotect(protectedValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }

        var separator = payload.IndexOf(':');
        if (separator <= 0) return false;

        if (!long.TryParse(payload.AsSpan(0, separator), out var issuedAtUnix))
            return false;

        // 10-minute window matches the cookie lifetime — guard against clock skew abuse.
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnix);
        if (DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromMinutes(10))
            return false;

        var storedState = payload[(separator + 1)..];
        return CryptographicallyEqual(storedState, presentedState);
    }

    private static bool CryptographicallyEqual(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
