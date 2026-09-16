using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Media.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Media.Services;

public interface IMediaStore
{
    /// <summary>
    /// Stores bytes content-addressed by their SHA-256 hash and records (or
    /// reuses) a <see cref="MediaAsset"/> row for them. Uploading the same
    /// bytes twice is a no-op past the first call: the same asset, hash, and
    /// on-disk file are reused rather than duplicated.
    /// </summary>
    Task<MediaAsset> SaveAsync(
        byte[] bytes, string contentType, int width, int height, Guid createdById, CancellationToken ct = default);

    /// <summary>
    /// Opens a read stream for the asset's content-addressed file, or null if
    /// the file is missing. The caller already knows <paramref name="sha256"/>
    /// (it comes from the same row that resolved <paramref name="assetId"/>),
    /// so this never re-queries the database; <paramref name="assetId"/> is
    /// used only to identify the asset in the divergence-warning log below.
    /// </summary>
    Task<Stream?> OpenReadAsync(Guid assetId, string sha256, CancellationToken ct = default);
}

/// <summary>
/// Content-addressed file storage under a configured media root
/// (<c>Media:RootPath</c>) backed by <see cref="MediaAsset"/> rows. The
/// stored filename is always the SHA-256 of the bytes — the uploaded
/// filename never reaches the filesystem, so there is no path-traversal
/// surface.
/// </summary>
public sealed class MediaStore(AppDbContext db, IConfiguration configuration, ILogger<MediaStore> logger) : IMediaStore
{
    private string RootPath => configuration["Media:RootPath"]
        ?? throw new InvalidOperationException(
            "Media:RootPath must be configured. Set the Media__RootPath environment variable.");

    public async Task<MediaAsset> SaveAsync(
        byte[] bytes, string contentType, int width, int height, Guid createdById, CancellationToken ct = default)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var existing = await db.MediaAssets.FirstOrDefaultAsync(a => a.Sha256 == hash, ct);
        if (existing is not null)
            return existing;

        var root = RootPath;
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, hash);
        if (!File.Exists(path))
        {
            // Written beside the final name and moved into place, so a crash
            // mid-write can never leave a truncated file under a hash that
            // promises different bytes. Move-with-overwrite is atomic on the
            // same volume; two concurrent uploads of identical bytes both
            // produce the same content, so whichever lands last is fine.
            var tempPath = Path.Combine(root, $"{hash}.{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllBytesAsync(tempPath, bytes, ct);
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        var asset = new MediaAsset
        {
            Sha256 = hash,
            ContentType = contentType,
            Width = width,
            Height = height,
            ByteSize = bytes.LongLength,
            CreatedById = createdById,
        };
        db.MediaAssets.Add(asset);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another concurrent upload of the same bytes may have won the unique-index race.
            db.Entry(asset).State = EntityState.Detached;
            var raceWinner = await db.MediaAssets.FirstOrDefaultAsync(a => a.Sha256 == hash, ct);
            if (raceWinner is null)
                throw;
            return raceWinner;
        }

        return asset;
    }

    public Task<Stream?> OpenReadAsync(Guid assetId, string sha256, CancellationToken ct = default)
    {
        var path = Path.Combine(RootPath, sha256);
        if (!File.Exists(path))
        {
            logger.MediaFileMissing(assetId, sha256);
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }
}
