using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Users.Endpoints;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The self-service rename handler against real Postgres: what is stored,
/// what is audited, and which cached responses are evicted — only when the
/// name every surface shows actually changes.
/// </summary>
public class DisplayNameChangeTests : IntegrationTestBase
{
    private async Task<(User User, Guid PlayedEventId, Guid OtherEventId)> SeedAsync()
    {
        var db = CreateDbContext();
        var played = await Fixtures.AddEventAsync(db, withCompetitor: false);
        var other = await Fixtures.AddEventAsync(db, withCompetitor: false);
        var user = await Fixtures.AddUserAsync(db, "renamer");
        user.SetTwitchDisplayName("SolaireOfAstora");
        db.EventCompetitors.Add(new EventCompetitor { EventId = played.Event.Id, UserId = user.Id });
        await db.SaveChangesAsync();
        return (user, played.Event.Id, other.Event.Id);
    }

    private Task<Microsoft.AspNetCore.Http.IResult> RenameAsync(User user, string? displayName) =>
        UpdateMyDisplayNameEndpoint.Handle(
            new UpdateDisplayNameRequest(displayName), user.Principal(), CreateDbContext(), Audit, Cache,
            NullLogger<UpdateMyDisplayNameEndpoint>.Instance, default);

    [Fact]
    public async Task SettingAName_StoresTheOverride_AndReturnsIt()
    {
        var (user, _, _) = await SeedAsync();

        var result = await RenameAsync(user, "  Solaire ");

        result.Status().Should().Be(200);
        var body = result.Value<UserResponse>();
        body.DisplayName.Should().Be("Solaire");
        body.DisplayNameOverride.Should().Be("Solaire");
        body.TwitchDisplayName.Should().Be("SolaireOfAstora");
        var stored = await CreateDbContext().Users.SingleAsync(u => u.Id == user.Id);
        stored.DisplayName.Should().Be("Solaire");
    }

    [Fact]
    public async Task ARename_IsAudited_WithTheCallerAsActorAndSubject()
    {
        var (user, _, _) = await SeedAsync();

        await RenameAsync(user, "Solaire");

        var audit = await CreateDbContext().AuditLogs.SingleAsync(a => a.Type == AuditEventTypes.UserDisplayNameChanged);
        audit.ActorUserId.Should().Be(user.Id);
        audit.SubjectUserId.Should().Be(user.Id);
        audit.BeforeJson.Should().Contain("SolaireOfAstora");
        audit.AfterJson.Should().Contain("Solaire");
    }

    [Fact]
    public async Task ARename_EvictsTheScoreboardOfEveryEventTheUserPlays_AndTheCalendar()
    {
        var (user, playedEventId, otherEventId) = await SeedAsync();

        await RenameAsync(user, "Solaire");

        Cache.Evicted.Should().Contain(CacheTags.Scoreboard(playedEventId));
        Cache.Evicted.Should().Contain(CacheTags.Calendar);
        Cache.Evicted.Should().NotContain(CacheTags.Scoreboard(otherEventId));
    }

    [Fact]
    public async Task Clearing_RestoresTheTwitchName()
    {
        var (user, _, _) = await SeedAsync();
        await RenameAsync(user, "Solaire");

        var result = await RenameAsync(user, null);

        result.Value<UserResponse>().DisplayName.Should().Be("SolaireOfAstora");
        result.Value<UserResponse>().DisplayNameOverride.Should().BeNull();
        (await CreateDbContext().AuditLogs.CountAsync(a => a.Type == AuditEventTypes.UserDisplayNameChanged))
            .Should().Be(2);
    }

    [Fact]
    public async Task AnOverrideEqualToTheTwitchName_IsStored_ButNothingIsAuditedOrEvicted()
    {
        var (user, _, _) = await SeedAsync();

        var result = await RenameAsync(user, "SolaireOfAstora");

        result.Status().Should().Be(200);
        (await CreateDbContext().Users.SingleAsync(u => u.Id == user.Id)).DisplayNameOverride
            .Should().Be("SolaireOfAstora", "an explicit choice survives a later Twitch rename");
        (await CreateDbContext().AuditLogs.AnyAsync(a => a.Type == AuditEventTypes.UserDisplayNameChanged))
            .Should().BeFalse();
        Cache.Evicted.Should().BeEmpty();
    }

    [Fact]
    public async Task AnInvalidName_Is400_AndChangesNothing()
    {
        var (user, _, _) = await SeedAsync();

        var result = await RenameAsync(user, new string('x', 51));

        result.Status().Should().Be(400);
        result.ValidationErrors().Should().ContainKey("displayName");
        (await CreateDbContext().Users.SingleAsync(u => u.Id == user.Id)).DisplayName.Should().Be("SolaireOfAstora");
        Cache.Evicted.Should().BeEmpty();
    }
}
