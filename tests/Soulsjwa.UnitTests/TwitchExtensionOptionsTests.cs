using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Soulsjwa.Api.Features.TwitchExtension;
using Xunit;

namespace Soulsjwa.UnitTests;

public class TwitchExtensionOptionsTests
{
    private static readonly string SecretA = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
    private static readonly string SecretB = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray());

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void NothingConfigured_IsDisabled()
    {
        var options = TwitchExtensionOptions.FromConfiguration(Config());

        options.IsConfigured.Should().BeFalse();
        options.CanPush.Should().BeFalse();
        options.AllowedOrigins.Should().BeEmpty();
    }

    [Fact]
    public void ClientIdAndSecret_DecodesTheSecretAndDerivesTheCdnOrigin()
    {
        var options = TwitchExtensionOptions.FromConfiguration(Config(
            (TwitchExtensionOptions.ClientIdKey, "abc123"),
            (TwitchExtensionOptions.SecretKey, SecretA)));

        options.IsConfigured.Should().BeTrue();
        options.ClientId.Should().Be("abc123");
        options.SigningKeys.Should().ContainSingle().Which.Should().Equal(Enumerable.Repeat((byte)1, 32));
        options.AllowedOrigins.Should().Equal("https://abc123.ext-twitch.tv");
        options.CanPush.Should().BeFalse("no owner user id was given");
    }

    [Fact]
    public void SecretsList_KeepsEveryKey_ForRotation()
    {
        var options = TwitchExtensionOptions.FromConfiguration(Config(
            (TwitchExtensionOptions.ClientIdKey, "abc123"),
            (TwitchExtensionOptions.SecretsKey + ":0", SecretA),
            (TwitchExtensionOptions.SecretsKey + ":1", SecretB)));

        options.SigningKeys.Should().HaveCount(2);
    }

    [Fact]
    public void OwnerUserIdAndLocalTestOrigin_AreReadWhenGiven()
    {
        var options = TwitchExtensionOptions.FromConfiguration(Config(
            (TwitchExtensionOptions.ClientIdKey, "abc123"),
            (TwitchExtensionOptions.SecretKey, SecretA),
            (TwitchExtensionOptions.OwnerUserIdKey, "44322889"),
            (TwitchExtensionOptions.LocalTestOriginKey, "https://localhost:8080/")));

        options.CanPush.Should().BeTrue();
        options.OwnerUserId.Should().Be("44322889");
        options.AllowedOrigins.Should().Equal("https://abc123.ext-twitch.tv", "https://localhost:8080");
    }

    [Theory]
    [InlineData(TwitchExtensionOptions.ClientIdKey, "abc123", null, null)]
    [InlineData(null, null, TwitchExtensionOptions.SecretKey, "c2VjcmV0")]
    public void HalfAConfiguration_Throws(string? clientKey, string? clientValue, string? secretKey, string? secretValue)
    {
        var values = new List<(string, string?)>();
        if (clientKey is not null) values.Add((clientKey, clientValue));
        if (secretKey is not null) values.Add((secretKey, secretValue));

        var act = () => TwitchExtensionOptions.FromConfiguration(Config([.. values]));

        act.Should().Throw<InvalidOperationException>().WithMessage("*must be configured*");
    }

    [Fact]
    public void ASecretThatIsNotBase64_ThrowsNamingTheKey()
    {
        var act = () => TwitchExtensionOptions.FromConfiguration(Config(
            (TwitchExtensionOptions.ClientIdKey, "abc123"),
            (TwitchExtensionOptions.SecretKey, "not base64!!")));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{TwitchExtensionOptions.SecretKey}*base64*");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("evil.example.com")]
    public void AClientIdThatCouldEscapeTheOrigin_Throws(string clientId)
    {
        var act = () => TwitchExtensionOptions.FromConfiguration(Config(
            (TwitchExtensionOptions.ClientIdKey, clientId),
            (TwitchExtensionOptions.SecretKey, SecretA)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*alphanumeric*");
    }

    [Fact]
    public void ALocalTestOriginWithAPath_Throws()
    {
        var act = () => TwitchExtensionOptions.FromConfiguration(Config(
            (TwitchExtensionOptions.ClientIdKey, "abc123"),
            (TwitchExtensionOptions.SecretKey, SecretA),
            (TwitchExtensionOptions.LocalTestOriginKey, "https://localhost:8080/panel.html")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*origin*");
    }
}
