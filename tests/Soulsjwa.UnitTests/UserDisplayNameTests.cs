using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// <see cref="User.DisplayName"/> is the effective name every surface shows:
/// the manual override when one is set, the Twitch name otherwise.
/// </summary>
public class UserDisplayNameTests
{
    [Fact]
    public void WithoutAnOverride_TheTwitchNameIsShown()
    {
        var user = new User();

        user.SetTwitchDisplayName("SolaireOfAstora");

        user.TwitchDisplayName.Should().Be("SolaireOfAstora");
        user.DisplayNameOverride.Should().BeNull();
        user.DisplayName.Should().Be("SolaireOfAstora");
    }

    [Fact]
    public void AnOverride_TakesPrecedence()
    {
        var user = new User();
        user.SetTwitchDisplayName("SolaireOfAstora");

        user.SetDisplayNameOverride("Solaire");

        user.DisplayName.Should().Be("Solaire");
        user.TwitchDisplayName.Should().Be("SolaireOfAstora");
    }

    [Fact]
    public void ANewTwitchName_DoesNotReplaceTheOverride()
    {
        var user = new User();
        user.SetTwitchDisplayName("SolaireOfAstora");
        user.SetDisplayNameOverride("Solaire");

        user.SetTwitchDisplayName("SunBro");

        user.TwitchDisplayName.Should().Be("SunBro");
        user.DisplayName.Should().Be("Solaire");
    }

    [Fact]
    public void ClearingTheOverride_RestoresTheTwitchName()
    {
        var user = new User();
        user.SetTwitchDisplayName("SolaireOfAstora");
        user.SetDisplayNameOverride("Solaire");

        user.SetDisplayNameOverride(null);

        user.DisplayNameOverride.Should().BeNull();
        user.DisplayName.Should().Be("SolaireOfAstora");
    }
}
