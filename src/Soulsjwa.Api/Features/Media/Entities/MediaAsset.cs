using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Media.Entities;

/// <summary>
/// Metadata for a single uploaded, content-addressed image. The file itself
/// lives on disk under the configured media root, named by
/// <see cref="Sha256"/> — the uploaded filename never reaches the
/// filesystem, and two uploads of identical bytes share one row and one
/// file (see <c>MediaStore</c>).
/// </summary>
public class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>SHA-256 of the stored (post metadata-stripping) bytes, lowercase hex. Also the on-disk filename.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Verified from the file's own magic bytes — never the caller's declared Content-Type.</summary>
    public string ContentType { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }
    public long ByteSize { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
