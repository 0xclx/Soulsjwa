using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Media.Services;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Media.Endpoints;

public sealed record UploadMediaResponse(Guid AssetId, string Url, int Width, int Height, long ByteSize, string ContentType);

public class MediaEndpoint : IEndpoint
{
    /// <summary>Hard cap on the request body actually read, enforced while streaming.</summary>
    internal const int MaxUploadBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Transport-level override of Program.cs's 1 MiB global Kestrel request-body
    /// limit — a multipart/form-data envelope around a MaxUploadBytes file needs
    /// headroom above the file's own bytes for the boundary/headers.
    /// </summary>
    private const int MaxUploadRequestBytes = MaxUploadBytes + 64 * 1024;

    private static readonly Dictionary<string, string> ExtensionByContentType = new(StringComparer.Ordinal)
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpg",
        ["image/webp"] = "webp",
    };

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Prefix + "/uploads", UploadMedia)
            .WithName("UploadMedia")
            .WithSummary("Uploads an image, admin-only")
            .Produces<UploadMediaResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes))
            .RequireAdmin();

        app.MapGet(ApiRoutes.Prefix + "/media/{assetId:guid}", GetMedia)
            .WithName("GetMedia")
            .WithSummary("Serves a previously uploaded image, publicly and immutably cached")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
    }

    private static async Task<IResult> UploadMedia(
        HttpRequest request,
        ClaimsPrincipal principal,
        IMediaStore mediaStore,
        IAuditService audit,
        AppDbContext db,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal))
            return AdminAccess.Forbid();

        if (!request.HasFormContentType || !MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType))
            return Results.Problem(detail: "Expected multipart/form-data.", statusCode: StatusCodes.Status400BadRequest);

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrEmpty(boundary))
            return Results.Problem(detail: "Expected multipart/form-data.", statusCode: StatusCodes.Status400BadRequest);

        var reader = new MultipartReader(boundary, request.Body);
        byte[]? bytes = null;
        while (await reader.ReadNextSectionAsync(ct) is { } section)
        {
            if (!IsFileSection(section))
                continue;

            bytes = await ReadCappedAsync(section.Body, MaxUploadBytes, ct);
            break;
        }

        if (bytes is null)
            return Results.Problem(detail: "No file part found.", statusCode: StatusCodes.Status400BadRequest);
        if (bytes.Length > MaxUploadBytes)
            return Results.Problem(detail: "File exceeds the maximum upload size of 5 MiB.", statusCode: StatusCodes.Status413PayloadTooLarge);

        if (ImageValidator.SniffContainer(bytes) is null)
            return Results.Problem(detail: "Unsupported file type. Only PNG, JPEG, and WebP are accepted.", statusCode: StatusCodes.Status415UnsupportedMediaType);

        if (!ImageValidator.TryDetect(bytes, out var info))
            return Results.Problem(detail: "The file is not a valid image.", statusCode: StatusCodes.Status400BadRequest);

        var stripped = ImageValidator.StripMetadata(bytes, info!.Format);
        var callerId = EventOwnership.GetUserId(principal);
        var asset = await mediaStore.SaveAsync(stripped, info.ContentType, info.Width, info.Height, callerId, ct);

        audit.Log(
            db,
            AuditEventTypes.MediaUploaded,
            callerId,
            after: new { asset.Id, asset.ContentType, asset.Width, asset.Height, asset.ByteSize });
        await db.SaveChangesAsync(ct);

        var url = $"/api/v1/media/{asset.Id}";
        return Results.Created(url, new UploadMediaResponse(asset.Id, url, asset.Width, asset.Height, asset.ByteSize, asset.ContentType));
    }

    private static async Task<IResult> GetMedia(Guid assetId, AppDbContext db, IMediaStore mediaStore, CancellationToken ct)
    {
        var asset = await db.MediaAssets
            .Where(a => a.Id == assetId)
            .Select(a => new { a.ContentType, a.Sha256 })
            .FirstOrDefaultAsync(ct);
        if (asset is null)
            return Results.Problem(detail: "Media not found.", statusCode: StatusCodes.Status404NotFound);

        var stream = await mediaStore.OpenReadAsync(assetId, asset.Sha256, ct);
        if (stream is null)
            return Results.Problem(detail: "Media not found.", statusCode: StatusCodes.Status404NotFound);

        var extension = ExtensionByContentType.GetValueOrDefault(asset.ContentType, "bin");
        return Results.Stream(
                stream,
                asset.ContentType,
                enableRangeProcessing: true,
                entityTag: new EntityTagHeaderValue($"\"{asset.Sha256}\""))
            .AddMediaHeaders(assetId, extension);
    }

    private static bool IsFileSection(MultipartSection section)
    {
        var contentDisposition = section.GetContentDispositionHeader();
        return contentDisposition is not null
            && contentDisposition.DispositionType.Equals("form-data")
            && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
    }

    /// <summary>
    /// Stops the moment the cap is crossed — returning one byte more than
    /// <paramref name="maxBytes"/> — rather than reading the rest of a possibly
    /// much larger body. The caller checks <c>Length &gt; maxBytes</c> for a 413.
    /// </summary>
    private static async Task<byte[]?> ReadCappedAsync(Stream stream, int maxBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes)
                return buffer.ToArray();
        }
        return buffer.ToArray();
    }
}

file static class ResultExtensions
{
    /// <summary>
    /// An explicit (verified) Content-Type, nosniff, an inline filename hint, and
    /// a year-long immutable cache lifetime — content-addressed storage means the
    /// bytes at a given URL never change.
    /// </summary>
    public static IResult AddMediaHeaders(this IResult result, Guid assetId, string extension) =>
        new MediaHeadersResult(result, assetId, extension);

    private sealed class MediaHeadersResult(IResult inner, Guid assetId, string extension) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.XContentTypeOptions = "nosniff";
            httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{assetId}.{extension}\"";
            httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            await inner.ExecuteAsync(httpContext);
        }
    }
}
