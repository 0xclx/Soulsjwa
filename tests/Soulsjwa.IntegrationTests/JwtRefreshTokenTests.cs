using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Refresh-token issue, validation, rotation and reuse detection are rows and
/// updates, so they run on Postgres. Access-token generation, which touches
/// no database, stays in <c>Soulsjwa.UnitTests.JwtTokenServiceTests</c>.
/// </summary>
public class JwtRefreshTokenTests : IntegrationTestBase
{
    private static JwtTokenService CreateService(AppDbContext db, int refreshTokenDays = 30)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-that-is-long-enough-32chars!!",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = refreshTokenDays.ToString(),
            })
            .Build();
        return new JwtTokenService(config, db);
    }

    [Fact]
    public async Task GenerateRefreshToken_StoresAHashedRowForTheUser()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var service = CreateService(db);

        var (rawToken, entity) = await service.GenerateRefreshTokenAsync(user.Id);

        rawToken.Should().NotBeNullOrEmpty();
        var stored = await CreateDbContext().RefreshTokens.SingleAsync(t => t.Id == entity.Id);
        stored.UserId.Should().Be(user.Id);
        stored.TokenHash.Should().Be(entity.TokenHash).And.NotBe(rawToken);
        stored.TokenPrefix.Should().Be(entity.TokenPrefix).And.NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateRefreshToken_ExpiresAfterTheConfiguredDays()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);

        var (_, entity) = await CreateService(db, refreshTokenDays: 7).GenerateRefreshTokenAsync(user.Id);

        entity.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ValidateRefreshToken_ReturnsTheUserForALiveToken()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var service = CreateService(db);
        var (rawToken, _) = await service.GenerateRefreshTokenAsync(user.Id);

        var (validatedUser, token) = await CreateService(CreateDbContext()).ValidateRefreshTokenAsync(rawToken);

        validatedUser!.Id.Should().Be(user.Id);
        token.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateRefreshToken_ReturnsNullForAnUnknownToken()
    {
        var (user, token) = await CreateService(CreateDbContext()).ValidateRefreshTokenAsync("completely-invalid-token-value!!");

        user.Should().BeNull();
        token.Should().BeNull();
    }

    [Fact]
    public async Task RevokeRefreshToken_MakesItInvalid_AndAnUnknownTokenIsANoOp()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var service = CreateService(db);
        var (rawToken, _) = await service.GenerateRefreshTokenAsync(user.Id);

        await service.RevokeRefreshTokenAsync(rawToken);
        var act = () => service.RevokeRefreshTokenAsync("nonexistent-token-value!!!!!!!!");

        (await service.ValidateRefreshTokenAsync(rawToken)).User.Should().BeNull();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateRefreshToken_OnReplayOfARevokedToken_RevokesTheWholeFamily()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db);
        var service = CreateService(db);

        // Three live tokens; rotate the first (revoking it) as a legitimate refresh does.
        var (raw1, _) = await service.GenerateRefreshTokenAsync(user.Id);
        var (_, t2) = await service.GenerateRefreshTokenAsync(user.Id);
        var (_, t3) = await service.GenerateRefreshTokenAsync(user.Id);
        await service.RevokeRefreshTokenAsync(raw1);

        // Presenting the revoked token again is the replay pattern (RFC 6749
        // §10.4): rejected, and every sibling is revoked with it.
        var (replayUser, replayToken) = await service.ValidateRefreshTokenAsync(raw1);

        replayUser.Should().BeNull();
        replayToken.Should().BeNull();
        var read = CreateDbContext();
        (await read.RefreshTokens.SingleAsync(t => t.Id == t2.Id)).IsRevoked.Should().BeTrue();
        (await read.RefreshTokens.SingleAsync(t => t.Id == t3.Id)).IsRevoked.Should().BeTrue();
    }
}
