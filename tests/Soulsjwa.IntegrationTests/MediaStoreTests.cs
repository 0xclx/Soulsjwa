using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Media.Services;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Content-addressed storage: one file and one row per distinct byte content,
/// found again by hash, and a missing file reported as absent rather than
/// thrown. The row side (unique <c>Sha256</c>, the uploader FK) is real
/// Postgres; the file side is a throwaway directory per test.
/// </summary>
public class MediaStoreTests : IntegrationTestBase, IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "soulsjwa-media-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private IConfiguration ConfigWithRoot() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Media:RootPath"] = _tempRoot })
            .Build();

    private MediaStore MakeStore(AppDbContext db, IConfiguration? configuration = null) =>
        new(db, configuration ?? ConfigWithRoot(), NullLogger<MediaStore>.Instance);

    [Fact]
    public async Task SaveAsync_NewBytes_WritesFileAndCreatesAsset()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var store = MakeStore(db);
        byte[] bytes = [1, 2, 3, 4];

        var asset = await store.SaveAsync(bytes, "image/png", 10, 20, user.Id);

        asset.Sha256.Should().HaveLength(64);
        asset.ContentType.Should().Be("image/png");
        asset.Width.Should().Be(10);
        asset.Height.Should().Be(20);
        asset.ByteSize.Should().Be(4);
        File.Exists(Path.Combine(_tempRoot, asset.Sha256)).Should().BeTrue();
        Directory.GetFiles(_tempRoot).Should().HaveCount(1, "no temp file is left beside the final one");
        (await CreateDbContext().MediaAssets.CountAsync(a => a.Sha256 == asset.Sha256)).Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_IdenticalBytesTwice_ReusesAssetAndFile()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var store = MakeStore(db);
        byte[] bytes = [5, 6, 7, 8, 9];

        var first = await store.SaveAsync(bytes, "image/jpeg", 10, 10, user.Id);
        var second = await MakeStore(CreateDbContext()).SaveAsync(bytes, "image/jpeg", 10, 10, user.Id);

        second.Id.Should().Be(first.Id);
        (await CreateDbContext().MediaAssets.CountAsync(a => a.Sha256 == first.Sha256)).Should().Be(1);
        Directory.GetFiles(_tempRoot).Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveAsync_DifferentBytes_CreatesSeparateAssets()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var store = MakeStore(db);

        var a = await store.SaveAsync([1, 2, 3], "image/png", 1, 1, user.Id);
        var b = await store.SaveAsync([4, 5, 6], "image/png", 1, 1, user.Id);

        a.Id.Should().NotBe(b.Id);
        a.Sha256.Should().NotBe(b.Sha256);
        Directory.GetFiles(_tempRoot).Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAsync_ThenOpenReadAsync_RoundTripsOriginalBytes()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var store = MakeStore(db);
        byte[] bytes = [9, 8, 7, 6, 5];

        var asset = await store.SaveAsync(bytes, "image/webp", 1, 1, user.Id);
        await using var stream = await store.OpenReadAsync(asset.Id, asset.Sha256);
        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer);

        buffer.ToArray().Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task OpenReadAsync_UnknownSha256_ReturnsNull()
    {
        var store = MakeStore(CreateDbContext());
        (await store.OpenReadAsync(Guid.NewGuid(), new string('0', 64))).Should().BeNull();
    }

    [Fact]
    public async Task OpenReadAsync_RowExistsButFileMissing_ReturnsNullWithoutThrowing()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var store = MakeStore(db);
        var asset = await store.SaveAsync([1, 2, 3], "image/png", 1, 1, user.Id);
        File.Delete(Path.Combine(_tempRoot, asset.Sha256));

        (await store.OpenReadAsync(asset.Id, asset.Sha256)).Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithoutConfiguredRootPath_Throws()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var store = MakeStore(db, new ConfigurationBuilder().Build());

        var act = () => store.SaveAsync([1, 2, 3], "image/png", 1, 1, user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
