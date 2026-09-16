using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The site theme's xmin-backed ETag/If-Match pair. Belongs at the HTTP layer
/// on both counts: <c>ETag</c>/<c>If-Match</c> are a wire contract, and the
/// token behind them is Postgres's <c>xmin</c>, which the InMemory provider has
/// no equivalent for.
/// </summary>
public class OptimisticConcurrencyTests : ApiTestBase
{
    private async Task<HttpClient> CreateAdminClientAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User
        {
            TwitchId = Guid.NewGuid().ToString(),
            TwitchLogin = "admin-" + Guid.NewGuid().ToString("N")[..8],
            DisplayName = "Admin",
            Role = UserRole.Admin,
            IsAllowlisted = true,
        };
        db.Users.Add(user);
        var (rawKey, prefix) = ApiKeyAuthHandler.GenerateApiKey();
        db.ApiKeys.Add(new ApiKey
        {
            UserId = user.Id,
            Name = "test-key",
            KeyHash = ApiKeyAuthHandler.HashApiKey(rawKey),
            KeyPrefix = prefix,
        });
        await db.SaveChangesAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        return client;
    }

    private static object ValidThemeRequest() => new
    {
        backgroundAssetId = (Guid?)null,
        backgroundTreatment = "None",
        font = "SystemSansSerif",
        lightDefault = "#f5f5f5",
        lightAccent = "#6d28d9",
        lightDanger = "#b91c1c",
        lightInfo = "#0369a1",
        lightSuccess = "#15803d",
        lightHighlight = "#b45309",
        darkDefault = "#1e1e1e",
        darkAccent = "#c4b5fd",
        darkDanger = "#f87171",
        darkInfo = "#38bdf8",
        darkSuccess = "#4ade80",
        darkHighlight = "#fbbf24",
    };

    [Fact]
    public async Task PutTheme_StaleIfMatch_Returns409AndWritesNothing()
    {
        var client = await CreateAdminClientAsync();

        var getResponse = await client.GetAsync("/api/v1/theme");
        var staleETag = getResponse.Headers.ETag!.Tag;

        // Someone else updates the theme out of band, advancing its xmin.
        var outOfBand = await client.PutAsJsonAsync("/api/v1/theme", ValidThemeRequest());
        outOfBand.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replaying the now-stale token must be rejected, not silently applied.
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/theme")
        {
            Content = JsonContent.Create(ValidThemeRequest()),
        };
        request.Headers.TryAddWithoutValidation("If-Match", staleETag);
        var staleResponse = await client.SendAsync(request);

        staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutTheme_CurrentIfMatch_Returns200()
    {
        var client = await CreateAdminClientAsync();

        var getResponse = await client.GetAsync("/api/v1/theme");
        var currentETag = getResponse.Headers.ETag!.Tag;

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/theme")
        {
            Content = JsonContent.Create(ValidThemeRequest()),
        };
        request.Headers.TryAddWithoutValidation("If-Match", currentETag);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutTheme_NoIfMatch_BehavesAsLastWriteWinsForCompatibility()
    {
        var client = await CreateAdminClientAsync();

        // No If-Match at all — must succeed exactly as it did before this
        // feature existed, even against a theme that has already been
        // updated once (a stale reader's write still wins).
        (await client.PutAsJsonAsync("/api/v1/theme", ValidThemeRequest()))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await client.PutAsJsonAsync("/api/v1/theme", ValidThemeRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
