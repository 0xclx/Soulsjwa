using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Entities;
using Soulsjwa.Shared;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The entity model against the real schema: relationships load back, the
/// archived-event query filter applies, jsonb columns round-trip, the seeded
/// games are referenceable. These used to run on the InMemory provider, where
/// none of the mapping was actually exercised.
/// </summary>
public class EntityRoundTripTests : IntegrationTestBase
{
    /// <summary>Dark Souls: Remastered — seeded by the initial migration.</summary>
    private const int SeededGameId = GameIds.DarkSouls1Remastered;

    [Fact]
    public async Task Event_LoadsItsCompetitorsAndOwner()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = new Event { Name = "Test Event", Description = "A test event", CreatedById = owner.Id };
        db.Events.Add(ev);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = owner.Id });
        await db.SaveChangesAsync();

        var loaded = await CreateDbContext().Events
            .Include(e => e.Competitors)
            .Include(e => e.CreatedBy)
            .FirstAsync(e => e.Id == ev.Id);

        loaded.Competitors.Should().ContainSingle().Which.UserId.Should().Be(owner.Id);
        loaded.CreatedBy.TwitchLogin.Should().Be(owner.TwitchLogin);
    }

    [Fact]
    public async Task EventGame_LoadsItsObjectives_WithJsonbMetadata()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = new Event { Name = "Test Event", Description = "A test event", CreatedById = owner.Id };
        var eventGame = new EventGame { KnownGameId = SeededGameId };
        ev.EventGames.Add(eventGame);
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        db.Objectives.Add(new Objective
        {
            EventGameId = eventGame.Id,
            Name = "Kill Boss",
            Score = 100,
            Metadata = """{"boss":"Ornstein"}""",
        });
        await db.SaveChangesAsync();

        var loaded = await CreateDbContext().EventGames
            .Include(eg => eg.Objectives)
            .Include(eg => eg.KnownGame)
            .FirstAsync(eg => eg.Id == eventGame.Id);

        var objective = loaded.Objectives.Should().ContainSingle().Subject;
        objective.Name.Should().Be("Kill Boss");
        objective.Score.Should().Be(100);
        objective.Metadata.Should().Contain("Ornstein");
        loaded.KnownGame!.Id.Should().Be(SeededGameId);
    }

    [Fact]
    public async Task Completions_SumPerCompetitorIndependently()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 100);
        var other = await Fixtures.AddUserAsync(db, "other");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = other.Id });
        var second = await Fixtures.AddObjectiveAsync(db, f.Game, "Boss B", 200);
        db.CompletedObjectives.AddRange(
            new CompletedObjective { ObjectiveId = f.Objective.Id, UserId = f.Competitor.Id },
            new CompletedObjective { ObjectiveId = second.Id, UserId = f.Competitor.Id },
            new CompletedObjective { ObjectiveId = f.Objective.Id, UserId = other.Id });
        await db.SaveChangesAsync();

        var read = CreateDbContext();
        (await read.CompletedObjectives.Where(co => co.UserId == f.Competitor.Id).SumAsync(co => co.Objective.Score)).Should().Be(300);
        (await read.CompletedObjectives.Where(co => co.UserId == other.Id).SumAsync(co => co.Objective.Score)).Should().Be(100);
    }

    [Fact]
    public async Task Event_CanHaveSeveralGames()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = new Event { Name = "Multi-Game Event", Description = "Test", CreatedById = owner.Id };
        ev.EventGames.Add(new EventGame { KnownGameId = GameIds.DarkSouls1Remastered });
        ev.EventGames.Add(new EventGame { KnownGameId = GameIds.DarkSouls2Scholar });
        ev.EventGames.Add(new EventGame { CustomGameName = "Board game" });
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var loaded = await CreateDbContext().Events.Include(e => e.EventGames).FirstAsync(e => e.Id == ev.Id);

        loaded.EventGames.Should().HaveCount(3);
    }

    [Fact]
    public async Task PredefinedObjective_HasAGameAndNoEventGame()
    {
        var db = CreateDbContext();
        var objective = new Objective
        {
            GameId = SeededGameId,
            Name = "Fixture - Slain",
            Score = 10,
            Rule = """{">":[{"var":"g1_f16"},0]}""",
            IsPredefined = true,
            EventGameId = null,
        };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();

        var loaded = await CreateDbContext().Objectives.FirstAsync(o => o.Id == objective.Id);
        loaded.IsPredefined.Should().BeTrue();
        loaded.GameId.Should().Be(SeededGameId);
        loaded.EventGameId.Should().BeNull();
        loaded.Rule.Should().Contain("g1_f16");
        loaded.Metadata.Should().BeNull();
    }

    [Fact]
    public async Task ArchivedEvents_AreExcludedByTheQueryFilter()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var active = new Event { Name = "Active Event", Description = "Active", CreatedById = owner.Id };
        var archived = new Event { Name = "Archived Event", Description = "Archived", CreatedById = owner.Id, IsArchived = true };
        db.Events.AddRange(active, archived);
        await db.SaveChangesAsync();

        var read = CreateDbContext();
        (await read.Events.Where(e => e.CreatedById == owner.Id).Select(e => e.Name).ToListAsync())
            .Should().Equal("Active Event");
        (await read.Events.IgnoreQueryFilters().CountAsync(e => e.CreatedById == owner.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Game_RoundTripsConnectorFields()
    {
        var db = CreateDbContext();
        db.Games.Add(new Game
        {
            Id = 90002,
            Name = "Fixture Game",
            Description = "Test",
            ConnectorSupported = true,
            RequiredConnectorVersion = "1.0.0",
        });
        await db.SaveChangesAsync();

        var loaded = await CreateDbContext().Games.FirstAsync(g => g.Id == 90002);
        loaded.RequiredConnectorVersion.Should().Be("1.0.0");
        loaded.ConnectorSupported.Should().BeTrue();
    }
}
