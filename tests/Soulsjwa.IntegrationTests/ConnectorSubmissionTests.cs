using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Connector;
using Soulsjwa.Api.Features.Connector.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Api.Features.Games.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Shared;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// What a connector submission completes, fails, cascades onto other
/// competitors, and refuses — all a function of the payload and the rows, with
/// no HTTP involved. The API suite keeps the routes, the
/// anonymous-versus-authenticated split and one submission through real auth.
/// </summary>
public class ConnectorSubmissionTests : IntegrationTestBase
{
    /// <summary>Dark Souls: Remastered — seeded, and connector-supported.</summary>
    private const int SupportedGameId = 1;

    /// <summary>Asylum Demon, as the catalogue names its flag.</summary>
    private static readonly string FlagDataPoint =
        GameDataDefinitions.SoulMemoryFlagId(SupportedGameId, 16);

    private static readonly string MatchingRule =
        $$"""{">":[{"var":"{{FlagDataPoint}}"},0]}""";

    /// <summary>Fails a competitor as soon as one other competitor completes it.</summary>
    private const string CountOnlyFailRule = """{">=":[{"var":"competitorCompletions"},1]}""";

    [Fact]
    public async Task GetGameData_ForASupportedGame_ReturnsItsDataPoints()
    {
        var result = await ConnectorEndpoint.GetGameData(SupportedGameId, CreateDbContext(), default);

        var response = result.Value<ConnectorGameDataResponse>();
        response.GameId.Should().Be(SupportedGameId);
        response.DataPoints.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetGameData_ForAGameWithoutConnectorSupport_IsNotFound()
    {
        var db = CreateDbContext();
        var unsupported = new Game
        {
            Id = 90001,
            Name = "Fixture Unsupported Game",
            Description = "Test-only game with no connector support.",
            ConnectorSupported = false,
        };
        db.Games.Add(unsupported);
        await db.SaveChangesAsync();

        var result = await ConnectorEndpoint.GetGameData(unsupported.Id, CreateDbContext(), default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
        result.Detail().Should().NotBeNullOrWhiteSpace("every problem carries a detail");
    }

    [Fact]
    public async Task GetGameData_ForAGameThatDoesNotExist_IsNotFound()
    {
        var result = await ConnectorEndpoint.GetGameData(99999, CreateDbContext(), default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
        result.Detail().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetSupportedGames_ListsTheConnectorSupportedCatalogue()
    {
        var result = await ConnectorEndpoint.GetSupportedGames(CreateDbContext(), default);

        result.Value<List<ConnectorSupportedGameResponse>>()
            .Should().NotBeEmpty().And.Contain(g => g.Id == SupportedGameId);
    }

    [Fact]
    public async Task GetEvents_ListsTheCallersEventsWithGamesInSortOrder()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db);
        db.EventGames.Add(new EventGame { EventId = f.Event.Id, CustomGameName = "Second", SortOrder = 5 });
        db.EventGames.Add(new EventGame { EventId = f.Event.Id, CustomGameName = "First", SortOrder = -1 });
        await db.SaveChangesAsync();

        var result = await ConnectorEndpoint.GetEvents(f.Competitor.Principal(), CreateDbContext(), default);

        var ev = result.Value<List<ConnectorEventResponse>>().Should().ContainSingle().Subject;
        ev.Id.Should().Be(f.Event.Id);
        ev.Games.Select(g => g.GameName).Should().ContainInOrder("First", "Dark Souls: Remastered", "Second");
        ev.Games.Single(g => g.EventGameId == f.Game.Id).ConnectorSupported.Should().BeTrue();
        ev.Games.Single(g => g.EventGameId == f.Game.Id).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetEvents_ForAUserWhoCompetesNowhere_IsEmpty()
    {
        var db = CreateDbContext();
        await AddConnectorEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await ConnectorEndpoint.GetEvents(stranger.Principal(), CreateDbContext(), default);

        result.Value<List<ConnectorEventResponse>>().Should().BeEmpty();
    }

    [Fact]
    public async Task Submit_ForAGameThatIsNotInTheEvent_IsNotFound()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db);

        var result = await SubmitAsync(db, f.Event.Id, Guid.NewGuid(), f.Competitor, "{}");

        result.Status().Should().Be(StatusCodes.Status404NotFound);
        result.Detail().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Submit_ByANonCompetitor_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await SubmitAsync(db, f.Event.Id, f.Game.Id, stranger, "{}");

        // Consistent with the manual-completion path, which already answered
        // 403 for this condition.
        result.Status().Should().Be(StatusCodes.Status403Forbidden);
        result.Detail().Should().Contain("competitor");
    }

    [Fact]
    public async Task Submit_WithMalformedData_IsRejected()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, rule: MatchingRule);

        var result = await SubmitAsync(db, f.Event.Id, f.Game.Id, f.Competitor, "not-json");

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Submit_WithDataOverTheByteCap_IsRejected()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db);
        var oversized = "{\"pad\":\"" + new string('x', ConnectorSubmissionValidator.MaxDataBytes) + "\"}";

        var result = await SubmitAsync(db, f.Event.Id, f.Game.Id, f.Competitor, oversized);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Submit_WithMoreDataPointsThanTheCap_IsRejected()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, custom: true);
        var tooWide = Enumerable.Range(0, ConnectorSubmissionValidator.MaxDataPoints + 1)
            .ToDictionary(i => $"k{i}", i => i);

        var result = await SubmitAsync(db, f.Event.Id, f.Game.Id, f.Competitor, JsonSerializer.Serialize(tooWide));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Submit_WithARealisticallyLargePayload_IsAccepted()
    {
        // Near the top of the largest known real catalogue (Elden Ring
        // Memory, ~7,550 data points): the caps must stay above what a real
        // connector actually sends.
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, custom: true);
        var realistic = Enumerable.Range(0, 7550)
            .ToDictionary(i => $"g9_f{1000000 + i}", i => i % 2);

        var result = await SubmitAsync(db, f.Event.Id, f.Game.Id, f.Competitor, JsonSerializer.Serialize(realistic));

        result.Status().Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Submit_WithNoRuledObjectives_CompletesNothing()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db);

        var result = await SubmitAsync(db, f.Event.Id, f.Game.Id, f.Competitor, "{}");

        result.Value<ConnectorDataSubmissionResult>().CompletedCount.Should().Be(0);
    }

    [Fact]
    public async Task Submit_WhoseRuleMatches_CompletesTheObjectiveOnceOnly()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, rule: MatchingRule);
        var data = $$"""{"{{FlagDataPoint}}":1}""";

        var first = await SubmitAsync(db, f.Event.Id, f.Game.Id, f.Competitor, data);
        var second = await SubmitAsync(CreateDbContext(), f.Event.Id, f.Game.Id, f.Competitor, data);

        first.Value<ConnectorDataSubmissionResult>().CompletedCount.Should().Be(1);
        second.Value<ConnectorDataSubmissionResult>().CompletedCount.Should().Be(0,
            "the same data twice is the connector's normal polling, not a second completion");
        (await CreateDbContext().CompletedObjectives.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Submit_CarriesTheInGameTimeFromThePayloadOntoTheCompletion()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, rule: MatchingRule);

        // DS1 is SoulMemory-backed, so its in-game clock arrives as
        // g1_game_time_ms and is already in milliseconds.
        var result = await SubmitAsync(
            db, f.Event.Id, f.Game.Id, f.Competitor,
            $$"""{"{{FlagDataPoint}}":1,"g1_game_time_ms":42000}""");

        result.Status().Should().Be(StatusCodes.Status200OK);
        (await CreateDbContext().CompletedObjectives.SingleAsync(c => c.UserId == f.Competitor.Id))
            .InGameTimeMs.Should().Be(42000);
    }

    [Fact]
    public async Task Submit_WhoseFailRuleMatches_FailsTheObjective()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, failRule: MatchingRule);

        var result = await SubmitAsync(db, f.Event.Id, f.Game.Id, f.Competitor, $$"""{"{{FlagDataPoint}}":1}""");

        var counts = result.Value<ConnectorDataSubmissionResult>();
        counts.CompletedCount.Should().Be(0);
        counts.FailedCount.Should().Be(1);
        (await CreateDbContext().FailedObjectives.SingleAsync()).ObjectiveId.Should().Be(f.Objective!.Id);
    }

    [Fact]
    public async Task Submit_WhereBothRulesMatch_CompletesRatherThanFails()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, rule: MatchingRule, failRule: MatchingRule);

        var result = await SubmitAsync(db, f.Event.Id, f.Game.Id, f.Competitor, $$"""{"{{FlagDataPoint}}":1}""");

        var counts = result.Value<ConnectorDataSubmissionResult>();
        counts.CompletedCount.Should().Be(1);
        counts.FailedCount.Should().Be(0, "a completion beats a failure when the data satisfies both");
        var after = CreateDbContext();
        (await after.CompletedObjectives.CountAsync()).Should().Be(1);
        (await after.FailedObjectives.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Submit_CascadesACountOnlyFailureOntoTheOtherCompetitors()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, rule: MatchingRule, failRule: CountOnlyFailRule);
        var rival = await Fixtures.AddUserAsync(db, "rival");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = rival.Id });
        await db.SaveChangesAsync();

        await SubmitAsync(CreateDbContext(), f.Event.Id, f.Game.Id, f.Competitor, $$"""{"{{FlagDataPoint}}":1}""");

        var after = CreateDbContext();
        (await after.CompletedObjectives.SingleAsync()).UserId.Should().Be(f.Competitor.Id);
        var failure = await after.FailedObjectives.SingleAsync();
        failure.ObjectiveId.Should().Be(f.Objective!.Id);
        failure.UserId.Should().Be(rival.Id);
    }

    [Fact]
    public async Task Submit_IgnoresACompletionLeftBehindByARemovedCompetitor()
    {
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, failRule: CountOnlyFailRule);
        var departed = await Fixtures.AddUserAsync(db, "departed");
        db.CompletedObjectives.Add(new CompletedObjective
        {
            ObjectiveId = f.Objective!.Id,
            UserId = departed.Id,
        });
        await db.SaveChangesAsync();

        var result = await SubmitAsync(CreateDbContext(), f.Event.Id, f.Game.Id, f.Competitor, "{}");

        result.Value<ConnectorDataSubmissionResult>().FailedCount.Should().Be(0,
            "competitorCompletions counts current competitors, not everyone who ever completed it");
    }

    [Fact]
    public async Task Submit_EvaluatesEveryObjectiveInOneGo()
    {
        // The payload and each rule are parsed at most once per request rather
        // than once per objective. Fifty objectives must still give the same
        // answers as evaluating each on its own.
        var db = CreateDbContext();
        var f = await AddConnectorEventAsync(db, custom: true, withObjective: false);
        const int objectiveCount = 50;
        for (var i = 0; i < objectiveCount; i++)
        {
            db.Objectives.Add(new Objective
            {
                EventGameId = f.Game.Id,
                Name = $"Objective {i}",
                Score = 1,
                // Even-indexed objectives complete; the odd ones' data points
                // are absent from the submission.
                Rule = $$"""{"==":[{"var":"flag_{{i}}"},1]}""",
            });
        }
        await db.SaveChangesAsync();
        var data = Enumerable.Range(0, objectiveCount).Where(i => i % 2 == 0)
            .ToDictionary(i => $"flag_{i}", _ => 1);

        var result = await SubmitAsync(
            CreateDbContext(), f.Event.Id, f.Game.Id, f.Competitor, JsonSerializer.Serialize(data));

        var counts = result.Value<ConnectorDataSubmissionResult>();
        counts.CompletedCount.Should().Be(objectiveCount / 2);
        counts.FailedCount.Should().Be(0);
        (await CreateDbContext().CompletedObjectives.CountAsync()).Should().Be(objectiveCount / 2);
    }

    private sealed record ConnectorEvent(User Competitor, Event Event, EventGame Game, Objective? Objective);

    /// <summary>
    /// A running event with one enabled game the caller competes in, and
    /// optionally one objective carrying <paramref name="rule"/> and
    /// <paramref name="failRule"/>.
    /// </summary>
    private static async Task<ConnectorEvent> AddConnectorEventAsync(
        AppDbContext db,
        string? rule = null,
        string? failRule = null,
        bool custom = false,
        bool withObjective = true)
    {
        var owner = await Fixtures.AddUserAsync(db, "owner", UserRole.Admin);
        var competitor = await Fixtures.AddUserAsync(db, "comp");
        var ev = new Event { Name = "connector event", CreatedById = owner.Id, IsStarted = true };
        var game = custom
            ? new EventGame { CustomGameName = "Custom Game", IsEnabled = true }
            : new EventGame { KnownGameId = SupportedGameId, IsEnabled = true };
        ev.EventGames.Add(game);
        db.Events.Add(ev);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = competitor.Id });
        await db.SaveChangesAsync();

        Objective? objective = null;
        if (withObjective && (rule is not null || failRule is not null))
        {
            objective = new Objective
            {
                EventGameId = game.Id,
                Name = "Connector objective",
                Score = 5,
                Rule = rule,
                FailRule = failRule,
            };
            db.Objectives.Add(objective);
            await db.SaveChangesAsync();
        }

        return new ConnectorEvent(competitor, ev, game, objective);
    }

    private Task<IResult> SubmitAsync(
        AppDbContext db, Guid eventId, Guid eventGameId, User caller, string data) =>
        ConnectorEndpoint.SubmitGameData(
            eventId, eventGameId, new ConnectorSubmissionPayload(data), caller.Principal(),
            db, Cache, NullLogger<ConnectorEndpoint>.Instance, default);
}
