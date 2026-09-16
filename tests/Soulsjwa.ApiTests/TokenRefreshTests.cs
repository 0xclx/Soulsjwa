using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

public class TokenRefreshTests : ApiTestBase
{
    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

        var user = new User
        {
            TwitchId = "test123",
            TwitchLogin = "testuser",
            DisplayName = "Test User",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (rawToken, _) = await jwtService.GenerateRefreshTokenAsync(user.Id);
        Client.DefaultRequestHeaders.Add("Cookie", $"refresh_token={rawToken}");

        var response = await Client.PostAsync("/api/v1/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body.Should().ContainKey("accessToken");
        body.Should().NotContainKey("refreshToken");

        response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        setCookieHeaders.Should().NotBeNull();
        setCookieHeaders!.Any(x => x.Contains("refresh_token=", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Remove("Cookie");
        Client.DefaultRequestHeaders.Add("Cookie", "refresh_token=invalid-token");
        var response = await Client.PostAsync("/api/v1/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
