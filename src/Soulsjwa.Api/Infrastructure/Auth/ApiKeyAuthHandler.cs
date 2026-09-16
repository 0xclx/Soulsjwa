using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Infrastructure.Auth;

public class ApiKeyAuthHandler(
    IOptionsMonitor<ApiKeyAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext dbContext)
    : AuthenticationHandler<ApiKeyAuthOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    /// <summary>Shared with Program.cs's scheme selector and the Swagger security definition so they can't drift apart.</summary>
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var apiKeyValues))
            return AuthenticateResult.NoResult();

        var apiKey = apiKeyValues.FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
            return AuthenticateResult.NoResult();

        if (!TryExtractPrefix(apiKey, out var prefix))
            return AuthenticateResult.Fail("Invalid API key format");

        var keyHash = HashApiKey(apiKey);

        var storedKey = await dbContext.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyPrefix == prefix && k.KeyHash == keyHash && !k.IsRevoked);

        if (storedKey is null)
            return AuthenticateResult.Fail("Invalid API key");

        if (storedKey.ExpiresAt.HasValue && storedKey.ExpiresAt.Value < DateTime.UtcNow)
            return AuthenticateResult.Fail("API key expired");

        // Re-validate the owner on every use: de-allowlisting does not touch
        // ApiKeys. Fail (not NoResult) so the request is rejected outright, with a
        // message indistinguishable from the other failures so this can't become an
        // oracle for "this key exists but is de-allowlisted".
        if (!storedKey.User.IsAllowlisted)
            return AuthenticateResult.Fail("Invalid API key");

        // Throttle LastUsedAt updates to reduce DB writes (every 5 minutes)
        if (!storedKey.LastUsedAt.HasValue ||
            (DateTime.UtcNow - storedKey.LastUsedAt.Value).TotalMinutes >= 5)
        {
            storedKey.LastUsedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, storedKey.UserId.ToString()),
            new Claim("twitch_login", storedKey.User.TwitchLogin),
            new Claim("display_name", storedKey.User.DisplayName),
            new Claim("role", storedKey.User.Role.ToString()),
            new Claim("auth_method", "api_key"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    private const string SchemePrefix = "sk_";

    /// <summary>
    /// Splits on the fixed <see cref="SchemePrefix"/> length rather than
    /// <c>Split('_')</c>: the key part is Base64Url-encoded, and that alphabet
    /// includes <c>_</c>, so a literal split would truncate or reject keys.
    /// </summary>
    private static bool TryExtractPrefix(string apiKey, out string prefix)
    {
        if (!apiKey.StartsWith(SchemePrefix, StringComparison.Ordinal))
        {
            prefix = string.Empty;
            return false;
        }
        var keyPart = apiKey[SchemePrefix.Length..];
        prefix = keyPart[..Math.Min(8, keyPart.Length)];
        return true;
    }

    public static string HashApiKey(string apiKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(hash);
    }

    public static (string FullKey, string Prefix) GenerateApiKey()
    {
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        var keyPart = Base64Url.EncodeToString(keyBytes)[..32];
        var prefix = keyPart[..8];
        var fullKey = $"{SchemePrefix}{keyPart}";
        return (fullKey, prefix);
    }
}

public class ApiKeyAuthOptions : AuthenticationSchemeOptions { }
