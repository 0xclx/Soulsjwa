using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Soulsjwa.Api.Features.TwitchExtension;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// The token contract with Twitch, exercised end to end in memory: a token
/// signed the way Twitch signs one validates under the parameters the bearer
/// scheme is registered with, and the claims come out under Twitch's names.
/// </summary>
public class TwitchExtensionAuthTests
{
    private static readonly byte[] CurrentKey = Enumerable.Repeat((byte)7, 32).ToArray();
    private static readonly byte[] PreviousKey = Enumerable.Repeat((byte)9, 32).ToArray();
    private static readonly byte[] StrangerKey = Enumerable.Repeat((byte)3, 32).ToArray();

    private const string ChannelId = "12345";

    private static TwitchExtensionOptions Options(params byte[][] keys) =>
        TwitchExtensionOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(
                keys.Select((k, i) => new KeyValuePair<string, string?>(
                    $"{TwitchExtensionOptions.SecretsKey}:{i}", Convert.ToBase64String(k)))
                .Prepend(new KeyValuePair<string, string?>(TwitchExtensionOptions.ClientIdKey, "clientid")))
            .Build());

    private static async Task<ClaimsPrincipal?> ValidateAsync(string token, TwitchExtensionOptions options)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token, TwitchExtensionAuth.BuildValidationParameters(options));
        return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
    }

    [Fact]
    public async Task AViewerToken_ValidatesAndExposesTheChannelRoleAndOpaqueId()
    {
        var token = TwitchExtensionAuth.CreateToken(
            CurrentKey, ChannelId, TwitchExtensionAuth.Roles.Viewer,
            DateTime.UtcNow.AddMinutes(5), opaqueUserId: "U777");

        var principal = await ValidateAsync(token, Options(CurrentKey));

        principal.Should().NotBeNull();
        TwitchExtensionAuth.GetChannelId(principal!).Should().Be(ChannelId);
        TwitchExtensionAuth.GetOpaqueUserId(principal!).Should().Be("U777");
        TwitchExtensionAuth.IsBroadcaster(principal!).Should().BeFalse();
        principal!.IsInRole(TwitchExtensionAuth.Roles.Viewer).Should().BeTrue("the scheme maps `role` as the role claim");
    }

    [Fact]
    public async Task ABroadcasterToken_IsRecognisedAsTheBroadcaster()
    {
        var token = TwitchExtensionAuth.CreateToken(
            CurrentKey, ChannelId, TwitchExtensionAuth.Roles.Broadcaster, DateTime.UtcNow.AddMinutes(5));

        var principal = await ValidateAsync(token, Options(CurrentKey));

        TwitchExtensionAuth.IsBroadcaster(principal!).Should().BeTrue();
        principal!.IsInRole(TwitchExtensionAuth.Roles.Broadcaster).Should().BeTrue();
    }

    [Fact]
    public async Task ATokenSignedWithThePreviousSecret_StillValidatesDuringRotation()
    {
        var token = TwitchExtensionAuth.CreateToken(
            PreviousKey, ChannelId, TwitchExtensionAuth.Roles.Viewer, DateTime.UtcNow.AddMinutes(5));

        (await ValidateAsync(token, Options(CurrentKey, PreviousKey))).Should().NotBeNull();
        (await ValidateAsync(token, Options(CurrentKey))).Should().BeNull("the previous secret was dropped");
    }

    [Fact]
    public async Task ATokenSignedWithAnotherSecret_IsRejected()
    {
        var token = TwitchExtensionAuth.CreateToken(
            StrangerKey, ChannelId, TwitchExtensionAuth.Roles.Broadcaster, DateTime.UtcNow.AddMinutes(5));

        (await ValidateAsync(token, Options(CurrentKey))).Should().BeNull();
    }

    [Fact]
    public async Task AnExpiredToken_IsRejected()
    {
        var token = TwitchExtensionAuth.CreateToken(
            CurrentKey, ChannelId, TwitchExtensionAuth.Roles.Viewer,
            DateTime.UtcNow.Subtract(TwitchExtensionAuth.ClockSkew).AddMinutes(-1));

        (await ValidateAsync(token, Options(CurrentKey))).Should().BeNull();
    }

    [Fact]
    public async Task AnExternalToken_CarriesTheOwnerIdAndPubSubPermissions()
    {
        var token = TwitchExtensionAuth.CreateToken(
            CurrentKey, TwitchExtensionAuth.GlobalChannelId, TwitchExtensionAuth.Roles.External,
            DateTime.UtcNow.AddMinutes(1),
            userId: "44322889",
            pubSubPerms: new Dictionary<string, string[]> { ["send"] = [TwitchExtensionAuth.PubSubTargets.Global] });

        var principal = await ValidateAsync(token, Options(CurrentKey));

        principal.Should().NotBeNull();
        principal!.FindFirstValue(TwitchExtensionAuth.Claims.UserId).Should().Be("44322889");
        principal.FindFirstValue(TwitchExtensionAuth.Claims.Role).Should().Be(TwitchExtensionAuth.Roles.External);
        principal.FindFirstValue(TwitchExtensionAuth.Claims.PubSubPerms).Should().Contain(TwitchExtensionAuth.PubSubTargets.Global);
    }

    [Fact]
    public void GetChannelId_IsNullForAPrincipalWithoutTheClaim()
    {
        TwitchExtensionAuth.GetChannelId(new ClaimsPrincipal(new ClaimsIdentity())).Should().BeNull();
    }
}
