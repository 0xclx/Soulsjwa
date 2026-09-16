using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// An event's games: adding, renaming, reordering, and the "exactly one enabled
/// game" switch — whose real arbiter is the partial unique index
/// <c>IX_EventGames_EventId_ActiveGame</c>, so these only mean anything against
/// real Postgres. The wire contract is
/// <c>Soulsjwa.ApiTests.EventGamesEndpointTests</c>.
/// </summary>
public class EventGameTests : IntegrationTestBase
{
    /// <summary>Dark Souls: Remastered — seeded by the initial migration.</summary>
    private const int SeededGameId = 1;

    [Fact]
    public async Task AddGame_AsTheOwner_AppendsItAtTheEndOfTheOrder()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = await AddEventAsync(db, owner, started: false);

        (await AddGameAsync(db, ev, owner)).Status().Should().Be(StatusCodes.Status201Created);
        (await AddGameAsync(CreateDbContext(), ev, owner)).Status().Should().Be(StatusCodes.Status201Created);

        var games = await CreateDbContext().EventGames
            .Where(eg => eg.EventId == ev.Id).OrderBy(eg => eg.SortOrder).ToListAsync();
        games.Should().HaveCount(2, "the same catalogue game twice is two independent entries");
        games.Select(g => g.SortOrder).Should().Equal(0, 1);
    }

    [Fact]
    public async Task AddGame_ByANonOwner_IsForbidden()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var stranger = await Fixtures.AddUserAsync(db, "stranger");
        var ev = await AddEventAsync(db, owner, started: false);

        var result = await AddGameAsync(db, ev, stranger);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task AddGame_ThatIsNotInTheCatalogue_IsNotFound()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = await AddEventAsync(db, owner, started: false);

        var result = await EventGamesEndpoint.AddGame(
            ev.Id, new AddGameToEventRequest(4242), owner.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AddCustomGame_WithoutAName_IsRejected()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = await AddEventAsync(db, owner, started: false);

        var result = await EventGamesEndpoint.AddCustomGame(
            ev.Id, new AddCustomGameToEventRequest("   ", null), owner.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey("Name");
    }

    [Fact]
    public async Task RemoveGame_AsTheOwner_DeletesIt()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false, enabled: false);

        var result = await EventGamesEndpoint.RemoveEventGame(
            f.Event.Id, f.Game.Id, f.Owner.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await CreateDbContext().EventGames.AnyAsync(eg => eg.Id == f.Game.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveGame_ThatIsNotInTheEvent_IsNotFound()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false, enabled: false);

        var result = await EventGamesEndpoint.RemoveEventGame(
            f.Event.Id, Guid.NewGuid(), f.Owner.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("addCustom")]
    [InlineData("remove")]
    public async Task ChangingTheGameListWhileTheEventRuns_Conflicts(string operation)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: true, enabled: false);

        var result = operation switch
        {
            "add" => await AddGameAsync(db, f.Event, f.Owner),
            "addCustom" => await EventGamesEndpoint.AddCustomGame(
                f.Event.Id, new AddCustomGameToEventRequest("Late entry", null), f.Owner.Principal(),
                db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default),
            _ => await EventGamesEndpoint.RemoveEventGame(
                f.Event.Id, f.Game.Id, f.Owner.Principal(),
                db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default),
        };

        result.Status().Should().Be(StatusCodes.Status409Conflict,
            "the game list is frozen once competitors are scoring against it");
    }

    [Fact]
    public async Task PatchGame_RenamingACustomGame_ChangesTheDisplayedName()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false, enabled: false);

        var result = await PatchAsync(db, f.Event, f.Game, f.Owner, new PatchEventGameRequest("New Name", null));

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await GameResponseAsync(f.Event, f.Game)).GameName.Should().Be("New Name");
    }

    [Fact]
    public async Task PatchGame_ClearingACustomGamesName_IsRejected()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false, enabled: false);

        var result = await PatchAsync(db, f.Event, f.Game, f.Owner, new PatchEventGameRequest("", null));

        result.Status().Should().Be(StatusCodes.Status400BadRequest,
            "a custom game has no catalogue name to fall back to");
        result.ValidationErrors().Should().ContainKey("Name");
    }

    [Fact]
    public async Task PatchGame_ClearingACatalogueGamesName_RevertsToTheCatalogueName()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = await AddEventAsync(db, owner, started: false);
        var game = new EventGame { EventId = ev.Id, KnownGameId = SeededGameId, CustomGameName = "Override" };
        db.EventGames.Add(game);
        await db.SaveChangesAsync();

        var result = await PatchAsync(CreateDbContext(), ev, game, owner, new PatchEventGameRequest("", null));

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        var response = await GameResponseAsync(ev, game);
        response.GameName.Should().Be(response.KnownGameName);
        response.GameName.Should().NotBe("Override");
    }

    [Fact]
    public async Task ReorderGames_WithTheFullSet_AppliesThatOrder()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = await AddEventAsync(db, owner, started: false);
        var ids = await AddCustomGamesAsync(db, ev, "First", "Second", "Third");

        var result = await EventGamesEndpoint.ReorderEventGames(
            ev.Id, new ReorderEventGamesRequest([ids[2], ids[0], ids[1]]), owner.Principal(),
            CreateDbContext(), Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        var ordered = await CreateDbContext().EventGames
            .Where(eg => eg.EventId == ev.Id).OrderBy(eg => eg.SortOrder).Select(eg => eg.Id).ToListAsync();
        ordered.Should().Equal(ids[2], ids[0], ids[1]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    public async Task ReorderGames_WithAnythingButTheFullSetExactlyOnce_IsRejected(string shape)
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = await AddEventAsync(db, owner, started: false);
        var ids = await AddCustomGamesAsync(db, ev, "First", "Second");
        List<Guid> payload = shape switch
        {
            "missing" => [ids[0]],
            "extra" => [ids[0], ids[1], Guid.NewGuid()],
            _ => [ids[0], ids[0]],
        };

        var result = await EventGamesEndpoint.ReorderEventGames(
            ev.Id, new ReorderEventGamesRequest(payload), owner.Principal(),
            CreateDbContext(), Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest,
            "a reorder is a permutation, so a partial or padded list is meaningless");
    }

    [Fact]
    public async Task ReorderObjectives_WithTheFullSet_AppliesThatOrder()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false, enabled: false);
        var second = await Fixtures.AddObjectiveAsync(db, f.Game, "Obj2");
        var third = await Fixtures.AddObjectiveAsync(db, f.Game, "Obj3");

        var result = await EventGamesEndpoint.ReorderObjectives(
            f.Event.Id, f.Game.Id,
            new ReorderObjectivesRequest([third.Id, f.Objective.Id, second.Id]), f.Owner.Principal(),
            CreateDbContext(), Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        var ordered = await CreateDbContext().Objectives
            .Where(o => o.EventGameId == f.Game.Id).OrderBy(o => o.SortOrder).Select(o => o.Id).ToListAsync();
        ordered.Should().Equal(third.Id, f.Objective.Id, second.Id);
    }

    [Fact]
    public async Task ReorderObjectives_WithIdsThatAreNotTheGames_IsRejected()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false, enabled: false);

        var result = await EventGamesEndpoint.ReorderObjectives(
            f.Event.Id, f.Game.Id, new ReorderObjectivesRequest([Guid.NewGuid()]), f.Owner.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Enable_BeforeTheEventStarts_Conflicts()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false, enabled: false);

        var result = await EnableAsync(db, f.Event, f.Game, f.Owner);

        result.Status().Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Enable_MarksItActive()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: false);

        var result = await EnableAsync(db, f.Event, f.Game, f.Owner);

        result.Status().Should().Be(StatusCodes.Status200OK);
        result.Value<List<EventGameResponse>>()
            .Single(g => g.EventGameId == f.Game.Id).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Enable_ASecondGame_DisablesTheFirst()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: true);
        var second = new EventGame { EventId = f.Event.Id, CustomGameName = "Second" };
        db.EventGames.Add(second);
        await db.SaveChangesAsync();

        var result = await EnableAsync(CreateDbContext(), f.Event, second, f.Owner);

        result.Status().Should().Be(StatusCodes.Status200OK);
        var games = result.Value<List<EventGameResponse>>();
        games.Single(g => g.EventGameId == f.Game.Id).IsEnabled.Should().BeFalse();
        games.Single(g => g.EventGameId == second.Id).IsEnabled.Should().BeTrue();
        games.Count(g => g.IsEnabled).Should().Be(1);
    }

    [Fact]
    public async Task Enable_ByANonOwner_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: false);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await EnableAsync(db, f.Event, f.Game, stranger);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Enable_ByAnAdminWhoDoesNotOwnTheEvent_IsAllowed()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: false);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await EnableAsync(db, f.Event, f.Game, admin);

        result.Status().Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Enable_TwoGamesConcurrently_LeavesExactlyOneEnabled()
    {
        // IX_EventGames_EventId_ActiveGame is the arbiter under a race — the
        // loser must get 409, not a silent second enabled row or a 500 out of
        // undocumented EF statement ordering.
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: false);
        var second = new EventGame { EventId = f.Event.Id, CustomGameName = "Second" };
        db.EventGames.Add(second);
        await db.SaveChangesAsync();

        var results = await Task.WhenAll(
            EnableAsync(CreateDbContext(), f.Event, f.Game, f.Owner),
            EnableAsync(CreateDbContext(), f.Event, second, f.Owner));

        results.Select(r => r.Status()).Should().OnlyContain(
            status => status == StatusCodes.Status200OK || status == StatusCodes.Status409Conflict);
        results.Should().Contain(r => r.Status() == StatusCodes.Status200OK);
        (await CreateDbContext().EventGames.CountAsync(eg => eg.EventId == f.Event.Id && eg.IsEnabled))
            .Should().Be(1);
    }

    [Fact]
    public async Task Disable_AnAlreadyDisabledGame_IsANoOp()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: false);

        var result = await EventGamesEndpoint.DisableEventGame(
            f.Event.Id, f.Game.Id, f.Owner.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
    }

    private static async Task<Event> AddEventAsync(AppDbContext db, User owner, bool started)
    {
        var ev = new Event { Name = "test event", CreatedById = owner.Id, IsStarted = started };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    private static async Task<List<Guid>> AddCustomGamesAsync(AppDbContext db, Event ev, params string[] names)
    {
        var games = names.Select((name, index) => new EventGame
        {
            EventId = ev.Id,
            CustomGameName = name,
            SortOrder = index,
        }).ToList();
        db.EventGames.AddRange(games);
        await db.SaveChangesAsync();
        return games.Select(g => g.Id).ToList();
    }

    private Task<IResult> AddGameAsync(AppDbContext db, Event ev, User caller) =>
        EventGamesEndpoint.AddGame(
            ev.Id, new AddGameToEventRequest(SeededGameId), caller.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

    private Task<IResult> PatchAsync(
        AppDbContext db, Event ev, EventGame game, User caller, PatchEventGameRequest request) =>
        EventGamesEndpoint.PatchEventGame(
            ev.Id, game.Id, request, caller.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

    private Task<IResult> EnableAsync(AppDbContext db, Event ev, EventGame game, User caller) =>
        EventGamesEndpoint.EnableEventGame(
            ev.Id, game.Id, caller.Principal(),
            db, Audit, Cache, NullLogger<EventGamesEndpoint>.Instance, default);

    /// <summary>
    /// The game as the event payload presents it, which is where the
    /// name-resolution rules are visible: <c>GameName</c> is the override if
    /// there is one and the catalogue name otherwise.
    /// </summary>
    private async Task<EventGameResponse> GameResponseAsync(Event ev, EventGame game)
    {
        var result = await EventsEndpoint.GetEvent(
            ev.Id.ToString(), HandlerHarness.Anonymous(), CreateDbContext(), default);
        return result.Value<EventResponse>().Games.Single(g => g.EventGameId == game.Id);
    }
}
