using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary><c>PATCH /admin/users/{id}/display-name</c> over the wire.</summary>
public class AdminUsersDisplayNameEndpointTests : ApiTestBase
{
    private static string Route(Guid id) => $"/api/v1/admin/users/{id}/display-name";

    [Fact]
    public async Task Admin_Renames_AndTheListShowsIt()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var (target, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "target", UserRole.User);
        var admin = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await admin.PatchAsJsonAsync(Route(target.Id), new { displayName = "Siegmeyer" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("displayName").GetString().Should().Be("Siegmeyer");
        body.GetProperty("displayNameOverride").GetString().Should().Be("Siegmeyer");
        body.GetProperty("twitchDisplayName").GetString().Should().Be(target.TwitchLogin);

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/users/?search={target.TwitchLogin}");
        var row = list.GetProperty("items").EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == target.Id);
        row.GetProperty("displayNameOverride").GetString().Should().Be("Siegmeyer");
        row.GetProperty("twitchDisplayName").GetString().Should().Be(target.TwitchLogin);
    }

    [Fact]
    public async Task UnknownUser_Is404()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");

        var response = await TestAuth.CreateAuthenticatedClient(Factory, adminKey)
            .PatchAsJsonAsync(Route(Guid.NewGuid()), new { displayName = "Nobody" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_Is400()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var (target, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "target", UserRole.User);

        var response = await TestAuth.CreateAuthenticatedClient(Factory, adminKey)
            .PatchAsJsonAsync(Route(target.Id), new { displayName = new string('x', 51) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
