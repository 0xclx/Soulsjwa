using System.Security.Cryptography;
using System.Text;

namespace Soulsjwa.Api.Common;

/// <summary>
/// Digest-plus-length summary of a large text document, stored in audit rows in
/// place of its full content: proves that and when a document changed, without
/// copying up to 64 KiB per side into every edit's <c>AuditLogs</c> row.
/// </summary>
public static class AuditContentDigest
{
    public static object Of(string? content) => content is null
        ? new { Sha256 = (string?)null, Length = 0 }
        : new
        {
            Sha256 = (string?)Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
            Length = Encoding.UTF8.GetByteCount(content),
        };
}
