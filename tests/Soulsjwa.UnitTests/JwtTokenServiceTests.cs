using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.UnitTests;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(AppDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-that-is-long-enough-32chars!!",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "30",
            })
            .Build();
        return new JwtTokenService(config, db);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidJwt()
    {
        var db = TestHelper.CreateUnusedDbContext();
        var service = CreateService(db);
        var user = new User
        {
            Id = Guid.NewGuid(),
            TwitchId = "123",
            TwitchLogin = "testuser",
            DisplayName = "Test User",
        };

        var token = service.GenerateAccessToken(user);

        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Subject.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void ApiKeyHandler_GenerateApiKey_ShouldReturnValidKey()
    {
        var (fullKey, prefix) = ApiKeyAuthHandler.GenerateApiKey();

        fullKey.Should().StartWith("sk_");
        prefix.Should().HaveLength(8);
    }

    [Fact]
    public void ApiKeyHandler_HashApiKey_ShouldBeDeterministic()
    {
        var hash1 = ApiKeyAuthHandler.HashApiKey("test-key");
        var hash2 = ApiKeyAuthHandler.HashApiKey("test-key");

        hash1.Should().Be(hash2);
    }
}
