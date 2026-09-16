using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Two concurrent rotations of the same refresh-token cookie must not both
/// succeed — that would create two independently-valid token chains for one
/// login and silently defeat reuse detection. Needs the real Postgres provider:
/// the race is arbitrated by a conditional `UPDATE`, which the InMemory
/// provider cannot express.
/// </summary>
public class RefreshTokenRotationConcurrencyTests : ApiTestBase
{
    [Fact]
    public async Task ConcurrentRefresh_SameCookie_ExactlyOneSucceeds()
    {
        Guid userId;
        Guid originalTokenId;
        string rawToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

            var user = new User
            {
                TwitchId = $"tw_{Guid.NewGuid():N}",
                TwitchLogin = $"racer_{Guid.NewGuid():N}"[..20],
                DisplayName = "racer",
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;

            (rawToken, var originalToken) = await jwtService.GenerateRefreshTokenAsync(user.Id);
            originalTokenId = originalToken.Id;
        }

        // A client that does NOT manage cookies. The shared one does, so the
        // winner's Set-Cookie could enter the container while the second
        // request was in flight and the racers would no longer be presenting
        // the same token — the entire premise here.
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

        Task<HttpResponseMessage> SendRefreshAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
            request.Headers.Add("Cookie", $"refresh_token={rawToken}");
            return client.SendAsync(request);
        }

        var results = await Task.WhenAll(SendRefreshAsync(), SendRefreshAsync());

        results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1,
            "exactly one racer should win the conditional revoke");
        results.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1,
            "the loser must abort rather than also issuing a new token chain");

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = await verifyDb.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();

        tokens.Should().HaveCount(2, "the original token plus exactly one replacement");
        tokens.Single(t => t.Id == originalTokenId).IsRevoked.Should().BeTrue(
            "the winner rotates the token it was presented");

        // Deliberately not "exactly one revoked" — two interleavings are both
        // legitimate, and which one happens is pure timing:
        //
        //  - the loser reaches TryRevokeForRotationAsync before the winner
        //    commits, loses the conditional update and aborts — the
        //    replacement survives, one token revoked;
        //  - the loser validates after the winner committed, so it presents an
        //    already-revoked token. That is refresh-token reuse, and
        //    JwtTokenService.ValidateRefreshTokenAsync answers it by revoking
        //    every token for the user (RFC 6749 §10.4) — two tokens revoked.
        tokens.Count(t => !t.IsRevoked).Should().BeLessThanOrEqualTo(1,
            "at most one refresh chain may survive a race for the same cookie");
    }
}
