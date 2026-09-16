using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Xunit;

namespace Soulsjwa.ApiTests;

public class FeatureFlagsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task QuickCompleteFlag_AdminCanReadAndUpdate()
    {
        const string key = Soulsjwa.Api.Features.Admin.FeatureFlagKeys.MyEventsQuickComplete;
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        using var client = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var initial = await client.GetFromJsonAsync<FeatureFlagDto>(
            $"/api/v1/admin/feature-flags/{key}");
        initial.Should().Be(new FeatureFlagDto(key, false));

        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/feature-flags/{key}",
            new { enabled = true });
        var updated = await response.Content.ReadFromJsonAsync<FeatureFlagDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Should().Be(new FeatureFlagDto(key, true));
    }

    [Fact]
    public async Task QuickCompleteFlag_RegularUserCannotManage()
    {
        const string key = Soulsjwa.Api.Features.Admin.FeatureFlagKeys.MyEventsQuickComplete;
        var (_, userKey) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "user", UserRole.User);
        using var client = TestAuth.CreateAuthenticatedClient(Factory, userKey);

        (await client.GetAsync($"/api/v1/admin/feature-flags/{key}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record FeatureFlagDto(string Key, bool Enabled);
}
