using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

public class UsersApiKeysEndpointTests : ApiTestBase
{
    [Fact]
    public async Task GetCurrentUser_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_Authenticated_ReturnsCallerProfile()
    {
        var (user, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserDto>();
        body!.Id.Should().Be(user.Id);
        body.TwitchLogin.Should().Be(user.TwitchLogin);
    }

    [Fact]
    public async Task CreateApiKey_BlankName_ReturnsValidationProblem()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync("/api/v1/users/me/api-keys",
            new { name = "", expiresAt = (DateTime?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateApiKey_PastExpiry_ReturnsValidationProblem()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync("/api/v1/users/me/api-keys",
            new { name = "x", expiresAt = DateTime.UtcNow.AddDays(-1) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateApiKey_ReturnsRawKeyOnceWithSkPrefix()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync("/api/v1/users/me/api-keys",
            new { name = "ci-key", expiresAt = (DateTime?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateApiKeyDto>();
        body!.Key.Should().StartWith("sk_");
        body.KeyPrefix.Should().HaveLength(8);
    }

    [Fact]
    public async Task ListApiKeys_DoesNotLeakHashAndOnlyReturnsCallerKeys()
    {
        var (_, aliceKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "alice");
        var (_, bobKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "bob");

        var bobClient = TestAuth.CreateAuthenticatedClient(Factory, bobKey);
        await bobClient.PostAsJsonAsync("/api/v1/users/me/api-keys",
            new { name = "bob-key", expiresAt = (DateTime?)null });

        var aliceClient = TestAuth.CreateAuthenticatedClient(Factory, aliceKey);
        var response = await aliceClient.GetAsync("/api/v1/users/me/api-keys");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var keys = await response.Content.ReadFromJsonAsync<List<ApiKeyDto>>();
        keys.Should().NotBeNull();
        keys!.Should().NotContain(k => k.Name == "bob-key");
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("keyHash", because: "list endpoint must not leak the hash");
        raw.Should().NotContain("\"key\"", because: "raw key is only returned on creation");
    }

    [Fact]
    public async Task DeleteApiKey_AnotherUsersKey_Returns404()
    {
        var (alice, aliceKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "alice");
        var (_, bobKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "bob");

        Guid aliceKeyId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            aliceKeyId = await db.ApiKeys.Where(k => k.UserId == alice.Id).Select(k => k.Id).FirstAsync();
        }

        var bobClient = TestAuth.CreateAuthenticatedClient(Factory, bobKey);
        var response = await bobClient.DeleteAsync($"/api/v1/users/me/api-keys/{aliceKeyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // DeleteApiKeyEndpoint returns a bare Results.NotFound() with no body
        // of its own; UseStatusCodePages()+AddProblemDetails() fill in the
        // RFC 7807 body.
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task CreateApiKey_EleventhActiveKey_Returns409()
    {
        // TestAuth.CreateUserWithApiKeyAsync already mints one key, so 9 more
        // reaches the cap of 10 active keys.
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        for (var i = 0; i < 9; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/users/me/api-keys",
                new { name = $"key-{i}", expiresAt = (DateTime?)null });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var eleventh = await client.PostAsJsonAsync("/api/v1/users/me/api-keys",
            new { name = "one-too-many", expiresAt = (DateTime?)null });

        eleventh.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateApiKey_AfterRevokingOneAtTheCap_Succeeds()
    {
        var (user, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        for (var i = 0; i < 9; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/users/me/api-keys",
                new { name = $"key-{i}", expiresAt = (DateTime?)null });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Revoke one of the newly-created keys, not the one authenticating
        // this client itself.
        Guid disposableKeyId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            disposableKeyId = await db.ApiKeys
                .Where(k => k.UserId == user.Id && k.Name == "key-0")
                .Select(k => k.Id)
                .FirstAsync();
        }
        (await client.DeleteAsync($"/api/v1/users/me/api-keys/{disposableKeyId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterRevoke = await client.PostAsJsonAsync("/api/v1/users/me/api-keys",
            new { name = "room-again", expiresAt = (DateTime?)null });

        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ListApiKeys_ExcludesRevokedByDefault_IncludeRevokedOptsIn()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        // Revoke a second, disposable key rather than the one authenticating
        // this client itself.
        var created = await (await client.PostAsJsonAsync("/api/v1/users/me/api-keys",
            new { name = "disposable", expiresAt = (DateTime?)null }))
            .Content.ReadFromJsonAsync<CreateApiKeyDto>();
        var keyId = created!.Id;
        (await client.DeleteAsync($"/api/v1/users/me/api-keys/{keyId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var defaultList = await (await client.GetAsync("/api/v1/users/me/api-keys"))
            .Content.ReadFromJsonAsync<List<ApiKeyDto>>();
        defaultList.Should().NotContain(k => k.Id == keyId);

        var withRevoked = await (await client.GetAsync("/api/v1/users/me/api-keys?includeRevoked=true"))
            .Content.ReadFromJsonAsync<List<ApiKeyDto>>();
        withRevoked.Should().Contain(k => k.Id == keyId && k.IsRevoked);
    }

    [Fact]
    public async Task DeleteApiKey_OwnKey_Returns204AndRevokesIt()
    {
        var (user, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        Guid keyId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            keyId = await db.ApiKeys.Where(k => k.UserId == user.Id).Select(k => k.Id).FirstAsync();
        }

        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var response = await client.DeleteAsync($"/api/v1/users/me/api-keys/{keyId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.ApiKeys.FirstAsync(k => k.Id == keyId)).IsRevoked.Should().BeTrue();
        }
    }

    private record UserDto(Guid Id, string TwitchLogin, string DisplayName, string? Email);
    private record ApiKeyDto(Guid Id, string Name, string KeyPrefix, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? LastUsedAt, bool IsRevoked);
    private record CreateApiKeyDto(Guid Id, string Name, string Key, string KeyPrefix, DateTime CreatedAt);
}
