using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Removing a user from the allowlist must revoke every credential that would
/// otherwise let them keep acting as themselves — refresh tokens, API keys, and
/// (via the JwtBearer OnTokenValidated hook) any still-valid bearer token.
/// </summary>
public class AllowlistEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Remove_RevokesApiKeysRefreshTokensAndBearerTokens_InOneTransaction()
    {
        var (admin, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var adminClient = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        Guid targetUserId;
        Guid allowlistEntryId;
        string targetRawApiKey;
        string targetAccessToken;

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var targetLogin = $"target_{suffix}";
            var target = new User
            {
                TwitchId = $"tw_{suffix}",
                TwitchLogin = targetLogin,
                DisplayName = targetLogin,
                IsAllowlisted = true,
            };
            db.Users.Add(target);
            db.AllowlistedTwitchLogins.Add(new AllowlistedTwitchLogin
            {
                TwitchLogin = targetLogin,
                AddedById = admin.Id,
            });
            await db.SaveChangesAsync();
            targetUserId = target.Id;
            allowlistEntryId = await db.AllowlistedTwitchLogins
                .Where(a => a.TwitchLogin == targetLogin)
                .Select(a => a.Id)
                .FirstAsync();

            var (rawKey, prefix) = ApiKeyAuthHandler.GenerateApiKey();
            db.ApiKeys.Add(new ApiKey
            {
                UserId = target.Id,
                Name = "target-key",
                KeyHash = ApiKeyAuthHandler.HashApiKey(rawKey),
                KeyPrefix = prefix,
            });
            await db.SaveChangesAsync();
            targetRawApiKey = rawKey;

            await jwtService.GenerateRefreshTokenAsync(target.Id);
            targetAccessToken = jwtService.GenerateAccessToken(target);
        }

        // Sanity: both credentials work before removal.
        (await SendWithApiKeyAsync(targetRawApiKey)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendWithBearerAsync(targetAccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        var removeResponse = await adminClient.DeleteAsync($"/api/v1/admin/allowlist/{allowlistEntryId}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var apiKey = await db.ApiKeys.FirstAsync(k => k.UserId == targetUserId);
            apiKey.IsRevoked.Should().BeTrue();

            var refreshToken = await db.RefreshTokens.FirstAsync(t => t.UserId == targetUserId);
            refreshToken.IsRevoked.Should().BeTrue();

            var audit = await db.AuditLogs
                .Where(a => a.Type == AuditEventTypes.AllowlistRemoved && a.SubjectUserId == targetUserId)
                .FirstAsync();
            audit.BeforeJson.Should().Contain("revokedApiKeyCount").And.Contain("1");
        }

        (await SendWithApiKeyAsync(targetRawApiKey)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendWithBearerAsync(targetAccessToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a bearer token minted before de-allowlisting must be rejected on its next use, not just at expiry");
    }

    private async Task<HttpResponseMessage> SendWithApiKeyAsync(string rawApiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.Add("X-Api-Key", rawApiKey);
        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWithBearerAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await Client.SendAsync(request);
    }
}
