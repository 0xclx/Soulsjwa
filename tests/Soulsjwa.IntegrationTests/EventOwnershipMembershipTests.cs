using System.Security.Claims;
using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Events.Entities;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// <see cref="EventOwnership.IsEventMemberAsync"/> is the gate protecting
/// per-event audit reads and archived-event visibility — only members may
/// see them. Membership is answered by rows, so this runs on Postgres.
/// </summary>
public class EventOwnershipMembershipTests : IntegrationTestBase
{
    [Fact]
    public async Task IsEventMember_Admin_True()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, withCompetitor: false);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        (await EventOwnership.IsEventMemberAsync(f.Event, admin.Principal(), CreateDbContext()))
            .Should().BeTrue();
    }

    [Fact]
    public async Task IsEventMember_Creator_True()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, withCompetitor: false);

        (await EventOwnership.IsEventMemberAsync(f.Event, f.Owner.Principal(), CreateDbContext()))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsEventMember_Competitor_True(bool isStreamer)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, withCompetitor: false);
        var competitor = await Fixtures.AddUserAsync(db, "comp");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = competitor.Id, IsStreamer = isStreamer });
        await db.SaveChangesAsync();

        (await EventOwnership.IsEventMemberAsync(f.Event, competitor.Principal(), CreateDbContext()))
            .Should().BeTrue();
    }

    [Fact]
    public async Task IsEventMember_DelegatedModerator_True()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, withCompetitor: false);
        var streamer = await Fixtures.AddUserAsync(db, "streamer");
        var moderator = await Fixtures.AddUserAsync(db, "mod");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = streamer.Id, IsStreamer = true });
        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = f.Event.Id,
            CompetitorUserId = streamer.Id,
            ModeratorUserId = moderator.Id,
        });
        await db.SaveChangesAsync();

        (await EventOwnership.IsEventMemberAsync(f.Event, moderator.Principal(), CreateDbContext()))
            .Should().BeTrue();
    }

    [Fact]
    public async Task IsEventMember_RandomUser_False()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        (await EventOwnership.IsEventMemberAsync(f.Event, stranger.Principal(), CreateDbContext()))
            .Should().BeFalse();
    }

    [Fact]
    public async Task IsEventMember_AnonymousPrincipal_False()
    {
        // GetEvent/ListEvents are AllowAnonymous, so this must not throw trying
        // to read a NameIdentifier claim that isn't there.
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        (await EventOwnership.IsEventMemberAsync(f.Event, new ClaimsPrincipal(new ClaimsIdentity()), CreateDbContext()))
            .Should().BeFalse();
    }
}
