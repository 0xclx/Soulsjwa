using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The events endpoints' HTTP contract: each route and verb exists, auth and
/// the owner/admin gates are wired in, and the payloads carry the field names
/// and shape the frontend reads — including the list row being a projection
/// with counts rather than the full graph. The rules (clamping, search,
/// archived visibility, alias uniqueness, tie-break modes, the stop
/// precondition, the featured-event race) live in
/// <c>Soulsjwa.IntegrationTests.EventLifecycleTests</c>.
/// </summary>
public class EventsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task ListEvents_DefaultPage_ReturnsPaginatedResponse()
    {
        var response = await Client.GetAsync("/api/v1/events/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PaginatedDto>();
        body.Should().NotBeNull();
        body!.Page.Should().Be(1);
        body.PageSize.Should().Be(20); // default
    }

    [Fact]
    public async Task GetEvent_NotFound_Returns404()
    {
        var response = await Client.GetAsync($"/api/v1/events/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateEvent_Unauthenticated_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/events/", new { name = "x", description = "y" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEvent_ValidPayload_Returns201AndOwner()
    {
        var (user, rawKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, rawKey);

        var response = await client.PostAsJsonAsync("/api/v1/events/",
            new { name = "Speedrun Tournament", description = "Race to the boss" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EventDto>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Speedrun Tournament");
        body.CreatedById.Should().Be(user.Id);
        body.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task PatchEvent_NotOwner_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "other", role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await SeedEventAsync(owner.Id, "original");

        var client = TestAuth.CreateAuthenticatedClient(Factory, otherKey);
        var response = await client.PatchAsJsonAsync($"/api/v1/events/{ev.Id}", new { name = "stolen" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchEvent_OwnerWithValidName_Returns200AndUpdates()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id, "before");

        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var response = await client.PatchAsJsonAsync($"/api/v1/events/{ev.Id}", new { name = "after" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EventDto>();
        body!.Name.Should().Be("after");
    }

    [Fact]
    public async Task ArchiveEvent_AsOwner_Returns204AndHidesFromList()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id, "to-archive");

        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var response = await client.PostAsync($"/api/v1/events/{ev.Id}/archive", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await Client.GetAsync("/api/v1/events/?pageSize=100");
        var body = await list.Content.ReadFromJsonAsync<PaginatedDtoOfEvent>();
        body!.Items.Select(i => i.Id).Should().NotContain(ev.Id);

        // Archived events are hidden from default lists but still addressable
        // by their members (owner/admin) so they can inspect and restore them
        // from the web UI.
        var get = await client.GetAsync($"/api/v1/events/{ev.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StartEvent_AsOwner_Returns204()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id, "to-start");

        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var response = await client.PostAsync($"/api/v1/events/{ev.Id}/start", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetFromJsonAsync<EventDto>($"/api/v1/events/{ev.Id}");
        get!.IsStarted.Should().BeTrue();
    }

    [Fact]
    public async Task StartEvent_NotOwner_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "other", role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await SeedEventAsync(owner.Id, "x");

        var client = TestAuth.CreateAuthenticatedClient(Factory, otherKey);
        var response = await client.PostAsync($"/api/v1/events/{ev.Id}/start", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FeatureEvent_NotAdmin_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "other", role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await SeedEventAsync(owner.Id, "x");

        var client = TestAuth.CreateAuthenticatedClient(Factory, otherKey);
        var response = await client.PostAsync($"/api/v1/events/{ev.Id}/feature", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFeaturedEvent_ReturnsTheFeaturedEvent()
    {
        var (admin, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(admin.Id, "featured-lookup");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        await client.PostAsync($"/api/v1/events/{ev.Id}/feature", null);

        var response = await Client.GetAsync("/api/v1/events/featured");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EventDto>();
        body!.Id.Should().Be(ev.Id);
        body.IsFeatured.Should().BeTrue();
    }

    [Fact]
    public async Task ListEvents_DoesNotReturnCompetitorsOrGamesArrays()
    {
        // The list view is a projection, not the full graph — pin the response
        // shape so it doesn't regress back to Include-based mapping.
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await SeedEventAsync(owner.Id, "shape-check");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = owner.Id });
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/v1/events/?pageSize=100");
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var item = json.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == ev.Id);

        item.TryGetProperty("competitors", out _).Should().BeFalse();
        item.TryGetProperty("games", out _).Should().BeFalse();
        item.GetProperty("competitorCount").GetInt32().Should().Be(1);
        item.GetProperty("gameCount").GetInt32().Should().Be(0);
    }

    private async Task<Event> SeedEventAsync(Guid ownerId, string name, string? urlAlias = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = name, UrlAlias = urlAlias, CreatedById = ownerId };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    private record PaginatedDto(int TotalCount, int Page, int PageSize);
    private record PaginatedDtoOfEvent(List<EventDto> Items, int TotalCount, int Page, int PageSize);
    private record EventDto(
        Guid Id,
        string Name,
        string? UrlAlias,
        string Description,
        Guid CreatedById,
        bool IsArchived,
        bool IsStarted,
        bool IsFeatured);
}
