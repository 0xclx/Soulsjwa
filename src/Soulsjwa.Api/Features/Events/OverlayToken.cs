using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Soulsjwa.Api.Features.Events;

/// <summary>
/// Mints and validates per-event overlay tokens, shaped
/// <c>ot_&lt;32 char random&gt;</c>. The leading characters of the random part
/// are stored in cleartext so the UI can show "ot_AbCd…" and lookups stay
/// indexable.
/// </summary>
public static class OverlayToken
{
    public const string Scheme = "ot";

    /// <summary>
    /// Preferred way to send the token: a header stays out of access logs,
    /// browser history and <c>Referer</c>, unlike the <c>?token=</c> query
    /// fallback. The query form remains supported for OBS browser sources,
    /// which are a bare URL and cannot set headers.
    /// </summary>
    public const string HeaderName = "X-Overlay-Token";

    /// <summary>Length of the random part (after the <c>ot_</c> prefix).</summary>
    public const int RandomLength = 32;

    /// <summary>Number of leading characters kept in cleartext for display + lookup.</summary>
    public const int PrefixLength = 8;

    /// <summary>
    /// Mints a token: the raw form is handed back to the caller exactly once,
    /// together with the displayable prefix.
    /// </summary>
    public static (string Raw, string Prefix) Generate()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var random = Base64Url.EncodeToString(bytes)[..RandomLength];
        var prefix = random[..PrefixLength];
        return ($"{SchemePrefix}{random}", prefix);
    }

    private const string SchemePrefix = $"{Scheme}_";

    /// <summary>
    /// Returns the leading characters of the random part, for an indexed lookup.
    /// Splits on the fixed <see cref="SchemePrefix"/> length rather than
    /// <c>Split('_')</c>: the random part is Base64Url-encoded and that alphabet
    /// includes <c>_</c>, so a literal split would reject or truncate tokens
    /// containing one.
    /// </summary>
    public static bool TryExtractPrefix(string token, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrEmpty(token)) return false;
        if (!token.StartsWith(SchemePrefix, StringComparison.Ordinal)) return false;
        var random = token[SchemePrefix.Length..];
        if (random.Length < PrefixLength) return false;
        prefix = random[..PrefixLength];
        return true;
    }

    /// <summary>SHA-256 of the raw token, base64-encoded. Mirrors <c>ApiKeyAuthHandler.HashApiKey</c>.</summary>
    public static string Hash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }
}
