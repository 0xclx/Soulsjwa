using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// "At most one enabled game per event" must be enforced by the database (a
/// partial unique index on EventGames(EventId) WHERE "IsEnabled"), not only by
/// EnableEventGame's disable-the-others logic: a write that bypasses the
/// endpoint must still be rejected.
/// </summary>
public class ActiveGameConstraintTests : IntegrationTestBase
{
    [Fact]
    public async Task EnablingASecondGameForTheSameEvent_IsRejectedByTheDatabase()
    {
        var db = CreateDbContext();

        var user = new User { TwitchId = Guid.NewGuid().ToString(), TwitchLogin = "owner", DisplayName = "Owner" };
        db.Users.Add(user);
        var ev = new Event { Name = "Constraint test event", CreatedById = user.Id };
        db.Events.Add(ev);
        var first = new EventGame { EventId = ev.Id, CustomGameName = "First", IsEnabled = true };
        db.EventGames.Add(first);
        await db.SaveChangesAsync();

        var second = new EventGame { EventId = ev.Id, CustomGameName = "Second", IsEnabled = true };
        db.EventGames.Add(second);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task TwoEventsCanEachHaveTheirOwnEnabledGame()
    {
        var db = CreateDbContext();

        var user = new User { TwitchId = Guid.NewGuid().ToString(), TwitchLogin = "owner2", DisplayName = "Owner" };
        db.Users.Add(user);
        var eventA = new Event { Name = "A", CreatedById = user.Id };
        var eventB = new Event { Name = "B", CreatedById = user.Id };
        db.Events.AddRange(eventA, eventB);
        db.EventGames.Add(new EventGame { EventId = eventA.Id, CustomGameName = "Game A", IsEnabled = true });
        db.EventGames.Add(new EventGame { EventId = eventB.Id, CustomGameName = "Game B", IsEnabled = true });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }
}
