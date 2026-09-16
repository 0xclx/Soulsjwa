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

public class RefreshTokenEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Refresh_NoCookie_Returns401()
    {
        var response = await Client.PostAsync("/api/v1/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_BlankCookie_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("Cookie");
        Client.DefaultRequestHeaders.Add("Cookie", "refresh_token=");
        var response = await Client.PostAsync("/api/v1/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        var (raw, tokenId) = await CreateUserAndRefreshTokenAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = await db.RefreshTokens.FirstAsync(r => r.Id == tokenId);
            t.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        Client.DefaultRequestHeaders.Remove("Cookie");
        Client.DefaultRequestHeaders.Add("Cookie", $"refresh_token={raw}");
        var response = await Client.PostAsync("/api/v1/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldOneIsRevokedAndNoLongerWorks()
    {
        var (oldRaw, oldTokenId) = await CreateUserAndRefreshTokenAsync();

        Client.DefaultRequestHeaders.Remove("Cookie");
        Client.DefaultRequestHeaders.Add("Cookie", $"refresh_token={oldRaw}");
        var first = await Client.PostAsync("/api/v1/auth/refresh", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Old refresh token must now be revoked in storage (rotation)
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.RefreshTokens.FirstAsync(r => r.Id == oldTokenId);
            stored.IsRevoked.Should().BeTrue("refresh-token rotation should revoke the consumed token");
        }

        var replay = await Client.PostAsync("/api/v1/auth/refresh", null);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_SetsNewRefreshTokenCookieWithCorrectAttributes()
    {
        var (raw, _) = await CreateUserAndRefreshTokenAsync();

        Client.DefaultRequestHeaders.Remove("Cookie");
        Client.DefaultRequestHeaders.Add("Cookie", $"refresh_token={raw}");
        var response = await Client.PostAsync("/api/v1/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookie = cookies!.First(c => c.StartsWith("refresh_token=", StringComparison.OrdinalIgnoreCase));
        cookie.Should().Contain("httponly", because: "refresh cookie must not be readable by JavaScript");
        cookie.Should().Contain("path=/api/v1/auth", because: "refresh cookie must be scoped to the auth API");
        cookie.Should().Contain("samesite=strict", because: "refresh cookie must be SameSite=Strict");
    }

    private async Task<(string RawToken, Guid TokenId)> CreateUserAndRefreshTokenAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            TwitchId = $"tw_{suffix}",
            TwitchLogin = $"u_{suffix}",
            DisplayName = $"u_{suffix}",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (raw, entity) = await jwtService.GenerateRefreshTokenAsync(user.Id);
        return (raw, entity.Id);
    }
}

public class RevokeTokenEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Revoke_NoCookie_StillReturns200AndClearsCookie()
    {
        var response = await Client.PostAsync("/api/v1/auth/revoke", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Should still emit a Set-Cookie that deletes the refresh_token (max-age 0)
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.Contains("refresh_token=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Revoke_ValidToken_RevokesItInDb()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            TwitchId = $"tw_{suffix}",
            TwitchLogin = $"u_{suffix}",
            DisplayName = $"u_{suffix}",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var (raw, entity) = await jwtService.GenerateRefreshTokenAsync(user.Id);

        Client.DefaultRequestHeaders.Remove("Cookie");
        Client.DefaultRequestHeaders.Add("Cookie", $"refresh_token={raw}");
        var response = await Client.PostAsync("/api/v1/auth/revoke", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.RefreshTokens.FirstAsync(t => t.Id == entity.Id)).IsRevoked.Should().BeTrue();
    }
}
