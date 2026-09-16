using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Soulsjwa.Api.Features.Auth;
using Xunit;

namespace Soulsjwa.UnitTests;

public class RefreshTokenCookieTests
{
    [Fact]
    public void Name_IsRefreshToken()
    {
        RefreshTokenCookie.Name.Should().Be("refresh_token");
    }

    [Fact]
    public void CreateOptions_IsHttpOnly()
    {
        var options = RefreshTokenCookie.CreateOptions(DateTime.UtcNow.AddDays(30));
        options.HttpOnly.Should().BeTrue();
    }

    [Fact]
    public void CreateOptions_IsSecure()
    {
        var options = RefreshTokenCookie.CreateOptions(DateTime.UtcNow.AddDays(30));
        options.Secure.Should().BeTrue();
    }

    [Fact]
    public void CreateOptions_SameSiteIsStrict()
    {
        var options = RefreshTokenCookie.CreateOptions(DateTime.UtcNow.AddDays(30));
        options.SameSite.Should().Be(SameSiteMode.Strict);
    }

    [Fact]
    public void CreateOptions_IsEssential()
    {
        var options = RefreshTokenCookie.CreateOptions(DateTime.UtcNow.AddDays(30));
        options.IsEssential.Should().BeTrue();
    }

    [Fact]
    public void CreateOptions_PathIsApiV1Auth()
    {
        var options = RefreshTokenCookie.CreateOptions(DateTime.UtcNow.AddDays(30));
        options.Path.Should().Be("/api/v1/auth");
    }

    [Fact]
    public void CreateOptions_ExpiresMatchesInput()
    {
        var expiry = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var options = RefreshTokenCookie.CreateOptions(expiry);
        options.Expires.Should().Be(new DateTimeOffset(expiry));
    }

    [Fact]
    public void CreateDeletionOptions_IsHttpOnly()
    {
        var options = RefreshTokenCookie.CreateDeletionOptions();
        options.HttpOnly.Should().BeTrue();
    }

    [Fact]
    public void CreateDeletionOptions_IsSecure()
    {
        var options = RefreshTokenCookie.CreateDeletionOptions();
        options.Secure.Should().BeTrue();
    }

    [Fact]
    public void CreateDeletionOptions_SameSiteIsStrict()
    {
        var options = RefreshTokenCookie.CreateDeletionOptions();
        options.SameSite.Should().Be(SameSiteMode.Strict);
    }

    [Fact]
    public void CreateDeletionOptions_PathIsApiV1Auth()
    {
        var options = RefreshTokenCookie.CreateDeletionOptions();
        options.Path.Should().Be("/api/v1/auth");
    }

    [Fact]
    public void CreateDeletionOptions_HasNoExpires()
    {
        var options = RefreshTokenCookie.CreateDeletionOptions();
        options.Expires.Should().BeNull();
    }
}
