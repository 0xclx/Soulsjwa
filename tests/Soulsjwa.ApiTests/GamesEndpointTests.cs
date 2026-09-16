using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Xunit;

namespace Soulsjwa.ApiTests;

public class GamesEndpointTests : ApiTestBase
{
    [Fact]
    public async Task CreateAndUpdateGame_AsAdmin_PersistsMetadata()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var name = $"Catalog game {Guid.NewGuid():N}";

        var create = await client.PostAsJsonAsync(
            "/api/v1/games", new { name, description = "Original description" });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<GameDto>();
        created.Should().NotBeNull();
        created!.ConnectorSupported.Should().BeFalse();

        var update = await client.PatchAsJsonAsync(
            $"/api/v1/games/{created.Id}",
            new { name = $"{name} updated", description = "Updated description" });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<GameDto>();
        updated!.Name.Should().Be($"{name} updated");
        updated.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task CreateGame_AsNonAdmin_Returns403()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, role: UserRole.User);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync(
            "/api/v1/games", new { name = $"Forbidden {Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateGame_BlankName_ReturnsValidationProblem()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync("/api/v1/games", new { name = " " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record GameDto(
        int Id,
        string Name,
        string Description,
        bool ConnectorSupported,
        string? RequiredConnectorVersion);
}
