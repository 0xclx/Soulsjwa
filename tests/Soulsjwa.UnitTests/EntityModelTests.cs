using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Entities;
using Xunit;

namespace Soulsjwa.UnitTests;

public class EntityModelTests
{
    #region RefreshToken IsActive/IsExpired

    [Fact]
    public void RefreshToken_IsActive_WhenNotRevokedAndNotExpired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false,
        };

        token.IsActive.Should().BeTrue();
        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void RefreshToken_IsNotActive_WhenExpired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            IsRevoked = false,
        };

        token.IsActive.Should().BeFalse();
        token.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void RefreshToken_IsNotActive_WhenRevoked()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
        };

        token.IsActive.Should().BeFalse();
        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void RefreshToken_IsNotActive_WhenBothRevokedAndExpired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            IsRevoked = true,
        };

        token.IsActive.Should().BeFalse();
    }

    #endregion

    #region User defaults

    [Fact]
    public void User_HasDefaultIdGenerated()
    {
        var user = new User();
        user.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void User_HasDefaultTimestamps()
    {
        var user = new User();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_HasEmptyCollectionsByDefault()
    {
        var user = new User();
        user.RefreshTokens.Should().BeEmpty();
        user.ApiKeys.Should().BeEmpty();
    }

    #endregion

    #region Event defaults

    [Fact]
    public void Event_HasDefaultIdGenerated()
    {
        var ev = new Event();
        ev.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Event_IsNotArchivedByDefault()
    {
        var ev = new Event();
        ev.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Event_IsNotStartedByDefault()
    {
        var ev = new Event();
        ev.IsStarted.Should().BeFalse();
    }

    [Fact]
    public void Event_HasEmptyCollectionsByDefault()
    {
        var ev = new Event();
        ev.Competitors.Should().BeEmpty();
        ev.EventGames.Should().BeEmpty();
    }

    #endregion

    #region EventGame defaults

    [Fact]
    public void Event_SharesPlacesOnEqualScoreByDefault()
    {
        // Runnable here, unlike the API-level assertion — the default lives in
        // the entity initializer, not a database default.
        new Event().TieBreakMode.Should().Be(TieBreakMode.SharedPlace);
    }

    [Fact]
    public void Event_AllowsTrialRunsByDefault()
    {
        new Event().AllowTrialRuns.Should().BeTrue();
    }

    [Fact]
    public void TrialRun_StartsNotStarted()
    {
        var run = new TrialRun();
        run.State.Should().Be(TrialRunState.NotStarted);
        run.StartedAt.Should().BeNull();
    }

    [Fact]
    public void EventGame_IsNotEnabledByDefault()
    {
        var eg = new EventGame();
        eg.IsEnabled.Should().BeFalse();
    }

    #endregion

    #region Objective defaults

    [Fact]
    public void Objective_HasDefaultIdGenerated()
    {
        var obj = new Objective();
        obj.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Objective_IsPredefined_DefaultsFalse()
    {
        var obj = new Objective();
        obj.IsPredefined.Should().BeFalse();
    }

    [Fact]
    public void Objective_MetadataAndRule_DefaultNull()
    {
        var obj = new Objective();
        obj.Metadata.Should().BeNull();
        obj.Rule.Should().BeNull();
    }

    #endregion

    #region CompletedObjective

    [Fact]
    public void CompletedObjective_HasDefaultId()
    {
        var co = new CompletedObjective();
        co.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CompletedObjective_HasCompletedAtTimestamp()
    {
        var co = new CompletedObjective();
        co.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region ApiKey entity

    [Fact]
    public void ApiKey_HasDefaultId()
    {
        var key = new ApiKey();
        key.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ApiKey_IsNotRevokedByDefault()
    {
        var key = new ApiKey();
        key.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void ApiKey_ExpiresAt_NullByDefault()
    {
        var key = new ApiKey();
        key.ExpiresAt.Should().BeNull();
    }

    #endregion
}
