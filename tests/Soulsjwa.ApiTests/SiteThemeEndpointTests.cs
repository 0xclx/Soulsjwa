using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Media.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

public class SiteThemeEndpointTests : ApiTestBase
{
    private static object ValidRequest(object? overrides = null)
    {
        var request = new Dictionary<string, object?>
        {
            ["backgroundAssetId"] = null,
            ["backgroundTreatment"] = "None",
            ["font"] = "SystemSansSerif",
            ["lightDefault"] = "#f5f5f5",
            ["lightAccent"] = "#6d28d9",
            ["lightDanger"] = "#b91c1c",
            ["lightInfo"] = "#0369a1",
            ["lightSuccess"] = "#15803d",
            ["lightHighlight"] = "#b45309",
            ["darkDefault"] = "#1e1e1e",
            ["darkAccent"] = "#c4b5fd",
            ["darkDanger"] = "#f87171",
            ["darkInfo"] = "#38bdf8",
            ["darkSuccess"] = "#4ade80",
            ["darkHighlight"] = "#fbbf24",
        };
        if (overrides is not null)
        {
            foreach (var prop in overrides.GetType().GetProperties())
                request[char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..]] = prop.GetValue(overrides);
        }
        return request;
    }

    [Fact]
    public async Task GetTheme_Anonymous_ReturnsSeededDefaults()
    {
        var response = await Client.GetAsync("/api/v1/theme");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SiteThemeDto>();
        body!.BackgroundTreatment.Should().Be("None");
        body.Font.Should().Be("SystemSansSerif");
        body.LightDefault.Should().Be("#f5f5f5");
        body.BackgroundAssetId.Should().BeNull();
        body.BackgroundUrl.Should().BeNull();
    }

    [Fact]
    public async Task PutTheme_NotAdmin_Returns403()
    {
        var (_, userKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "user", UserRole.User);
        using var client = TestAuth.CreateAuthenticatedClient(Factory, userKey);

        var response = await client.PutAsJsonAsync("/api/v1/theme", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutTheme_Admin_Returns200AndPersistsAndAudits()
    {
        var (admin, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await client.PutAsJsonAsync(
            "/api/v1/theme", ValidRequest(new { LightAccent = "#4c1d95" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SiteThemeDto>();
        body!.LightAccent.Should().Be("#4c1d95");

        var get = await Client.GetFromJsonAsync<SiteThemeDto>("/api/v1/theme");
        get!.LightAccent.Should().Be("#4c1d95");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditLogs
            .Where(a => a.Type == "site_theme.updated" && a.ActorUserId == admin.Id)
            .FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        audit!.AfterJson.Should().Contain("4c1d95");
    }

    [Fact]
    public async Task PutTheme_LowContrastPalette_Returns400NamingTheFailingPair()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        // Near-identical to LightDefault -- fails 4.5:1 contrast against it.
        var response = await client.PutAsJsonAsync(
            "/api/v1/theme", ValidRequest(new { LightAccent = "#f8f8f8" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.Should().Contain("Light.Accent");
        problem.Should().Contain("Light.Default");
    }

    [Fact]
    public async Task PutTheme_MalformedHex_Returns400()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await client.PutAsJsonAsync(
            "/api/v1/theme", ValidRequest(new { DarkHighlight = "not-a-colour" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutTheme_DisallowedFont_Returns400()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await client.PutAsJsonAsync("/api/v1/theme", ValidRequest(new { Font = "ComicSans" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutTheme_InvalidBackgroundTreatment_Returns400()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await client.PutAsJsonAsync(
            "/api/v1/theme", ValidRequest(new { BackgroundTreatment = "Stretch" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutTheme_NonexistentBackgroundAssetId_Returns400()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await client.PutAsJsonAsync(
            "/api/v1/theme", ValidRequest(new { BackgroundAssetId = Guid.NewGuid() }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutTheme_ExistingBackgroundAssetId_Succeeds()
    {
        var (admin, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = new MediaAsset
        {
            Sha256 = new string('a', 64),
            ContentType = "image/png",
            Width = 10,
            Height = 10,
            ByteSize = 100,
            CreatedById = admin.Id,
        };
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/theme", ValidRequest(new { BackgroundAssetId = asset.Id }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SiteThemeDto>();
        body!.BackgroundAssetId.Should().Be(asset.Id);
        body.BackgroundUrl.Should().Be($"/api/v1/media/{asset.Id}");
    }

    private sealed record SiteThemeDto(
        Guid? BackgroundAssetId,
        string? BackgroundUrl,
        string BackgroundTreatment,
        string Font,
        string LightDefault,
        string LightAccent,
        string LightDanger,
        string LightInfo,
        string LightSuccess,
        string LightHighlight,
        string DarkDefault,
        string DarkAccent,
        string DarkDanger,
        string DarkInfo,
        string DarkSuccess,
        string DarkHighlight,
        DateTime UpdatedAt);
}
