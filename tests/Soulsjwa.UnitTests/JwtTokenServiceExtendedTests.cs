using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// Access-token claims and validation parameters: pure signing, no database.
/// The refresh-token behaviour lives in
/// <c>Soulsjwa.IntegrationTests.JwtRefreshTokenTests</c>.
/// </summary>
public class JwtTokenServiceExtendedTests
{
    private static JwtTokenService CreateService(AppDbContext db, int accessTokenMinutes = 15, int refreshTokenDays = 30)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-that-is-long-enough-32chars!!",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenMinutes"] = accessTokenMinutes.ToString(),
                ["Jwt:RefreshTokenDays"] = refreshTokenDays.ToString(),
            })
            .Build();
        return new JwtTokenService(config, db);
    }

    private static User CreateTestUser(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TwitchId = "123",
        TwitchLogin = "testuser",
        DisplayName = "Test User",
    };

    [Fact]
    public void GenerateAccessToken_ContainsTwitchLoginClaim()
    {
        var db = TestHelper.CreateUnusedDbContext();
        var service = CreateService(db);
        var user = CreateTestUser();

        var token = service.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "twitch_login" && c.Value == "testuser");
    }

    [Fact]
    public void GenerateAccessToken_ContainsDisplayNameClaim()
    {
        var db = TestHelper.CreateUnusedDbContext();
        var service = CreateService(db);
        var user = CreateTestUser();

        var token = service.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "display_name" && c.Value == "Test User");
    }

    [Fact]
    public void GenerateAccessToken_HasCorrectIssuerAndAudience()
    {
        var db = TestHelper.CreateUnusedDbContext();
        var service = CreateService(db);
        var user = CreateTestUser();

        var token = service.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");
    }

    [Fact]
    public void GenerateAccessToken_HasJtiClaim()
    {
        var db = TestHelper.CreateUnusedDbContext();
        var service = CreateService(db);
        var user = CreateTestUser();

        var token = service.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void GenerateAccessToken_DifferentUsersProduceDifferentTokens()
    {
        var db = TestHelper.CreateUnusedDbContext();
        var service = CreateService(db);
        var user1 = CreateTestUser();
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            TwitchId = "456",
            TwitchLogin = "otheruser",
            DisplayName = "Other User",
        };

        var token1 = service.GenerateAccessToken(user1);
        var token2 = service.GenerateAccessToken(user2);

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GetValidationParameters_ReturnsCorrectSettings()
    {
        var db = TestHelper.CreateUnusedDbContext();
        var service = CreateService(db);

        var parameters = service.GetValidationParameters();

        parameters.ValidateIssuerSigningKey.Should().BeTrue();
        parameters.ValidateIssuer.Should().BeTrue();
        parameters.ValidIssuer.Should().Be("test-issuer");
        parameters.ValidateAudience.Should().BeTrue();
        parameters.ValidAudience.Should().Be("test-audience");
        parameters.ValidateLifetime.Should().BeTrue();
        parameters.ClockSkew.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Constructor_ThrowsWhenSecretNotConfigured()
    {
        var db = TestHelper.CreateUnusedDbContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var act = () => new JwtTokenService(config, db);

        act.Should().Throw<InvalidOperationException>();
    }
}
