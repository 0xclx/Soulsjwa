using FluentAssertions;
using Soulsjwa.Api.Infrastructure.Auth;
using Xunit;

namespace Soulsjwa.UnitTests;

public class JwtSecretValidationTests
{
    [Fact]
    public void Validate_MissingSecret_ReturnsError()
    {
        JwtSecretValidator.Validate(null, isDevelopment: false).Should().NotBeNull();
    }

    [Fact]
    public void Validate_EmptyStringSecret_ReturnsError()
    {
        // The unset-Compose-variable case: "${JWT_SECRET}" expands to "" when unset,
        // which is non-null and must still be rejected.
        JwtSecretValidator.Validate(string.Empty, isDevelopment: false).Should().NotBeNull();
    }

    [Fact]
    public void Validate_WhitespaceOnlySecret_ReturnsError()
    {
        JwtSecretValidator.Validate("   ", isDevelopment: false).Should().NotBeNull();
    }

    [Fact]
    public void Validate_ShortSecret_ReturnsErrorNamingMinimum()
    {
        var error = JwtSecretValidator.Validate("short-16-bytes!!", isDevelopment: false);
        error.Should().NotBeNull();
        error.Should().Contain("32");
    }

    [Fact]
    public void Validate_LongEnoughRandomSecret_Production_Passes()
    {
        var error = JwtSecretValidator.Validate("a-random-secret-that-is-definitely-long-enough-1234567890", isDevelopment: false);
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_DevelopmentPlaceholder_Production_ReturnsError()
    {
        var error = JwtSecretValidator.Validate("dev-secret-change-in-production-32chars!!", isDevelopment: false);
        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_DevelopmentPlaceholder_Development_Passes()
    {
        var error = JwtSecretValidator.Validate("dev-secret-change-in-production-32chars!!", isDevelopment: true);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("changeme")]
    [InlineData("secret")]
    public void Validate_KnownPlaceholder_Production_ReturnsError(string placeholder)
    {
        JwtSecretValidator.Validate(placeholder, isDevelopment: false).Should().NotBeNull();
    }
}
