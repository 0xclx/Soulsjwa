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

public class ApiKeyAuthHandlerAuthenticationTests : ApiTestBase
{
    [Fact]
    public async Task NoApiKey_OnProtectedEndpoint_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GarbageApiKey_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("X-Api-Key");
        Client.DefaultRequestHeaders.Add("X-Api-Key", "garbage-no-prefix");
        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownApiKeyWithSkPrefix_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("X-Api-Key");
        Client.DefaultRequestHeaders.Add("X-Api-Key", "sk_unknown_xxxxxxxxxxxxxxxxxxxxxxxxxx");
        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidApiKey_GrantsAccess()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        Client.DefaultRequestHeaders.Remove("X-Api-Key");
        Client.DefaultRequestHeaders.Add("X-Api-Key", key);

        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RevokedApiKey_Returns401()
    {
        var (user, rawKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.ApiKeys.FirstAsync(k => k.UserId == user.Id);
            entity.IsRevoked = true;
            await db.SaveChangesAsync();
        }

        Client.DefaultRequestHeaders.Remove("X-Api-Key");
        Client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredApiKey_Returns401()
    {
        var (user, rawKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.ApiKeys.FirstAsync(k => k.UserId == user.Id);
            entity.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        Client.DefaultRequestHeaders.Remove("X-Api-Key");
        Client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeallowlistedOwner_Returns401()
    {
        var (user, rawKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Id == user.Id);
            owner.IsAllowlisted = false;
            await db.SaveChangesAsync();
        }

        Client.DefaultRequestHeaders.Remove("X-Api-Key");
        Client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApiKeyHeader_RoutesThroughSmartScheme_NotJwt()
    {
        // With both schemes available, the presence of X-Api-Key must select
        // ApiKey rather than failing with "missing Bearer token".
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        Client.DefaultRequestHeaders.Remove("X-Api-Key");
        Client.DefaultRequestHeaders.Add("X-Api-Key", key);
        // Add a junk Bearer to prove it's ignored
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-real-jwt");

        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
