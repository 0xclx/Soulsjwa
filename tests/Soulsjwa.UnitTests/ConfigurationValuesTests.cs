using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Soulsjwa.Api.Common;
using Xunit;

namespace Soulsjwa.UnitTests;

public class ConfigurationValuesTests
{
    private static IConfiguration Config(string? value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimits:ConnectorPerMinute"] = value })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadPositiveInt_UsesTheDefault_WhenUnset(string? value)
    {
        ConfigurationValues.ReadPositiveInt(Config(value), "RateLimits:ConnectorPerMinute", 120).Should().Be(120);
    }

    [Fact]
    public void ReadPositiveInt_ReadsAValue()
    {
        ConfigurationValues.ReadPositiveInt(Config("45"), "RateLimits:ConnectorPerMinute", 120).Should().Be(45);
    }

    [Theory]
    [InlineData("12O")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.5")]
    public void ReadPositiveInt_NamesTheKeyAndValue_WhenInvalid(string value)
    {
        var act = () => ConfigurationValues.ReadPositiveInt(Config(value), "RateLimits:ConnectorPerMinute", 120);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RateLimits:ConnectorPerMinute*")
            .WithMessage($"*'{value}'*");
    }
}
