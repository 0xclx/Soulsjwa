using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The objective endpoints' HTTP contract: the routes, the admin and owner
/// gates as a client meets them, the ProblemDetails shape of a validation
/// failure, the FailRule contract's round trip through JSON and jsonb, and the
/// output caching — which only exists inside the HTTP pipeline, so nowhere else
/// can test it. The catalogue's own rules live in
/// <c>Soulsjwa.IntegrationTests.ObjectiveCatalogTests</c>.
/// </summary>
public class ObjectivesEndpointTests : ApiTestBase
{
    private const int SeededGameId = 1;

    [Fact]
    public async Task ListPredefined_ReturnsArray()
    {
        var response = await Client.GetAsync("/api/v1/objectives/predefined");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListPredefined_RepeatedRequest_HitsTheOutputCache()
    {
        var interceptor = new CommandCountingInterceptor();
        await using var factoryWithInterceptor = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(PostgresConnectionString).AddInterceptors(interceptor));
            });
        });
        using var client = factoryWithInterceptor.CreateClient();

        (await client.GetAsync($"/api/v1/objectives/predefined?gameId={SeededGameId}")).EnsureSuccessStatusCode();
        interceptor.Count.Should().BeGreaterThan(0);
        interceptor.Reset();

        (await client.GetAsync($"/api/v1/objectives/predefined?gameId={SeededGameId}")).EnsureSuccessStatusCode();

        interceptor.Count.Should().Be(0, "a repeated request within the cache window should be served from the output cache, not the database");
    }

    [Fact]
    public async Task CreatePredefined_EvictsTheOutputCacheForThatGame()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var name = $"cache-evict-{Guid.NewGuid():N}";

        var before = await Client.GetFromJsonAsync<PredefinedDto[]>(
            $"/api/v1/objectives/predefined?gameId={SeededGameId}");
        before.Should().NotContain(o => o.Name == name);

        var create = await client.PostAsJsonAsync("/api/v1/objectives/predefined", new
        {
            gameId = SeededGameId,
            name,
            score = 10,
            metadata = (string?)null,
            rule = (string?)null,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await Client.GetFromJsonAsync<PredefinedDto[]>(
            $"/api/v1/objectives/predefined?gameId={SeededGameId}");
        after.Should().Contain(o => o.Name == name);
    }

    [Fact]
    public async Task CreatePredefined_AsNonAdmin_Returns403()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services,
            role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync("/api/v1/objectives/predefined", new
        {
            gameId = SeededGameId,
            name = "not allowed",
            score = 5,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreatePredefined_MalformedRuleJson_ReturnsValidationProblemWithRuleKey()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync("/api/v1/objectives/predefined", new
        {
            gameId = SeededGameId,
            name = $"bad-rule-{Guid.NewGuid():N}",
            score = 5,
            rule = "not json",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Rule");
    }

    [Fact]
    public async Task ListEventGameObjectives_UnknownEventGame_Returns404()
    {
        var response = await Client.GetAsync(
            $"/api/v1/events/{Guid.NewGuid()}/games/{Guid.NewGuid()}/objectives/");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateEventGameObjective_NotOwner_Returns403()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "other", role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await SeedEventAsync(owner.Id);

        var addResp = await TestAuth.CreateAuthenticatedClient(Factory, ownerKey)
            .PostAsJsonAsync($"/api/v1/events/{ev.Id}/games", new { gameId = SeededGameId });
        var eventGameId = (await addResp.Content.ReadFromJsonAsync<CreatedGameResponse>())!.Id;

        var otherClient = TestAuth.CreateAuthenticatedClient(Factory, otherKey);
        var response = await otherClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/",
            new { name = "x", score = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateEventGameObjective_WithFailRule_RoundTripsInContractsAndSchema()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var addResp = await client.PostAsJsonAsync($"/api/v1/events/{ev.Id}/games", new { gameId = SeededGameId });
        var eventGameId = (await addResp.Content.ReadFromJsonAsync<CreatedGameResponse>())!.Id;

        const string failRule = """{">=":[{"var":"competitorCompletions"},2]}""";
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/",
            new { name = "Race objective", score = 5, rule = (string?)null, failRule });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ObjDto>();
        created!.FailRule.Should().Be(failRule);

        var list = await Client.GetFromJsonAsync<List<ObjDto>>(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/");
        var listed = list.Should().ContainSingle(o => o.Id == created.Id).Subject;
        JsonNode.DeepEquals(JsonNode.Parse(listed.FailRule!), JsonNode.Parse(failRule)).Should().BeTrue();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Objectives.FindAsync(created.Id);
        JsonNode.DeepEquals(JsonNode.Parse(stored!.FailRule!), JsonNode.Parse(failRule)).Should().BeTrue();
    }

    [Fact]
    public async Task PatchObjective_AsOwner_Returns200AndUpdates()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var addResp = await client.PostAsJsonAsync($"/api/v1/events/{ev.Id}/games", new { gameId = SeededGameId });
        var eventGameId = (await addResp.Content.ReadFromJsonAsync<CreatedGameResponse>())!.Id;
        var create = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/",
            new { name = "x", score = 5 });
        var created = await create.Content.ReadFromJsonAsync<ObjDto>();

        var patch = await client.PatchAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{created!.Id}",
            new { name = "renamed", score = 10 });

        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await patch.Content.ReadFromJsonAsync<ObjDto>();
        body!.Name.Should().Be("renamed");
        body.Score.Should().Be(10);
    }

    private record PredefinedDto(Guid Id, string Name, int Score, int? GameId);

    private async Task<Event> SeedEventAsync(Guid ownerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "test", CreatedById = ownerId };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    private record ObjDto(Guid Id, string Name, int Score, string? Metadata = null, bool IsPredefined = false, string? Rule = null, string? FailRule = null);
    private record CreatedGameResponse(Guid Id);
}
