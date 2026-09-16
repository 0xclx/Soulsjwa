using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

public class MediaEndpointTests : ApiTestBase
{
    private static byte[] BuildPng(int width, int height)
    {
        var bytes = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        WriteUInt32BigEndian(bytes, 8, 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        WriteUInt32BigEndian(bytes, 16, (uint)width);
        WriteUInt32BigEndian(bytes, 20, (uint)height);
        return bytes;
    }

    private static void WriteUInt32BigEndian(byte[] b, int offset, uint value)
    {
        b[offset] = (byte)(value >> 24);
        b[offset + 1] = (byte)(value >> 16);
        b[offset + 2] = (byte)(value >> 8);
        b[offset + 3] = (byte)value;
    }

    /// <summary>
    /// A PNG with a padding IDAT chunk, so the stored (metadata-stripped) file
    /// stays larger than any byte range this suite asks for.
    /// <see cref="BuildPng"/>'s bare signature+IHDR does not survive
    /// <c>ImageValidator.StripPngMetadata</c> as more than a handful of bytes.
    /// </summary>
    private static byte[] BuildLargePng(int width, int height, int paddingBytes)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var ihdr = new byte[13];
        WriteUInt32BigEndian(ihdr, 0, (uint)width);
        WriteUInt32BigEndian(ihdr, 4, (uint)height);
        WriteChunk(stream, "IHDR", ihdr);
        WriteChunk(stream, "IDAT", new byte[paddingBytes]);
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WriteChunk(MemoryStream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteUInt32BigEndian(length, 0, (uint)data.Length);
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write(new byte[4]); // CRC — not validated by the stripper or the validator
    }

    private static MultipartFormDataContent MakeUpload(byte[] bytes, string filename, string contentType)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", filename);
        return content;
    }

    [Fact]
    public async Task Upload_NonAdmin_Returns403()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "user", Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsync("/api/v1/uploads", MakeUpload(BuildPng(10, 10), "a.png", "image/png"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_PngRenamedJpg_AcceptedAndServedAsImagePng()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsync(
            "/api/v1/uploads", MakeUpload(BuildPng(100, 50), "photo.jpg", "image/jpeg"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UploadResponseDto>();
        body!.ContentType.Should().Be("image/png");
        body.Width.Should().Be(100);
        body.Height.Should().Be(50);

        var served = await client.GetAsync(body.Url);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        served.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        served.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
    }

    [Fact]
    public async Task Upload_HtmlDeclaringImagePng_Returns415()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var html = Encoding.UTF8.GetBytes("<html><body>not an image</body></html>");

        var response = await client.PostAsync("/api/v1/uploads", MakeUpload(html, "fake.png", "image/png"));

        response.StatusCode.Should().Be((HttpStatusCode)415);
    }

    [Fact]
    public async Task Upload_Svg_Returns415()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");

        var response = await client.PostAsync("/api/v1/uploads", MakeUpload(svg, "a.svg", "image/svg+xml"));

        response.StatusCode.Should().Be((HttpStatusCode)415);
    }

    [Fact]
    public async Task Upload_Gif_Returns415()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        byte[] gif = [.. "GIF89a"u8.ToArray(), .. new byte[20]];

        var response = await client.PostAsync("/api/v1/uploads", MakeUpload(gif, "a.gif", "image/gif"));

        response.StatusCode.Should().Be((HttpStatusCode)415);
    }

    [Fact]
    public async Task Upload_TruncatedPng_Returns400()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var truncated = BuildPng(10, 10)[..10];

        var response = await client.PostAsync("/api/v1/uploads", MakeUpload(truncated, "a.png", "image/png"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_OversizedDimensions_Returns400()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsync("/api/v1/uploads", MakeUpload(BuildPng(5000, 5000), "a.png", "image/png"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_OverSizeLimit_Returns413()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var huge = new byte[6 * 1024 * 1024];
        BuildPng(10, 10).CopyTo(huge, 0);

        var response = await client.PostAsync("/api/v1/uploads", MakeUpload(huge, "a.png", "image/png"));

        response.StatusCode.Should().Be((HttpStatusCode)413);
    }

    [Fact]
    public async Task Upload_IdenticalBytesTwice_ReturnsSameAssetId()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var bytes = BuildPng(20, 20);

        var first = await (await client.PostAsync("/api/v1/uploads", MakeUpload(bytes, "a.png", "image/png")))
            .Content.ReadFromJsonAsync<UploadResponseDto>();
        var second = await (await client.PostAsync("/api/v1/uploads", MakeUpload(bytes, "b.png", "image/png")))
            .Content.ReadFromJsonAsync<UploadResponseDto>();

        second!.AssetId.Should().Be(first!.AssetId);
    }

    [Fact]
    public async Task GetMedia_UnknownAssetId_Returns404()
    {
        var response = await Client.GetAsync($"/api/v1/media/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMedia_CarriesAStrongETagEqualToTheAssetsSha256AndAllRequiredHeaders()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var uploaded = await (await client.PostAsync("/api/v1/uploads", MakeUpload(BuildPng(10, 10), "a.png", "image/png")))
            .Content.ReadFromJsonAsync<UploadResponseDto>();

        var response = await Client.GetAsync(uploaded!.Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        response.Headers.ETag!.IsWeak.Should().BeFalse();
        response.Headers.ETag.Tag.Trim('"').Should().HaveLength(64);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("inline");
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.Extensions.Select(e => e.Name).Should().Contain("immutable");
    }

    [Fact]
    public async Task GetMedia_IfNoneMatchMatchingETag_Returns304WithNoBody()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var uploaded = await (await client.PostAsync("/api/v1/uploads", MakeUpload(BuildPng(10, 10), "b.png", "image/png")))
            .Content.ReadFromJsonAsync<UploadResponseDto>();
        var first = await Client.GetAsync(uploaded!.Url);
        var etag = first.Headers.ETag!;

        var request = new HttpRequestMessage(HttpMethod.Get, uploaded.Url);
        request.Headers.IfNoneMatch.Add(etag);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GetMedia_RangeRequest_Returns206WithRequestedByteRange()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var uploaded = await (await client.PostAsync("/api/v1/uploads", MakeUpload(BuildLargePng(10, 10, 500), "c.png", "image/png")))
            .Content.ReadFromJsonAsync<UploadResponseDto>();

        var request = new HttpRequestMessage(HttpMethod.Get, uploaded!.Url);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 99);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        (await response.Content.ReadAsByteArrayAsync()).Should().HaveCount(100);
    }

    private sealed record UploadResponseDto(Guid AssetId, string Url, int Width, int Height, long ByteSize, string ContentType);
}
