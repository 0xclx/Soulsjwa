using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Games.Services;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// What a predefined objective may contain, what a patch may change, who may
/// change it, and what importing into an event-game does. Rule and metadata
/// payloads are <c>jsonb</c>, so "is this accepted" is answered partly by the
/// validator and partly by Postgres — the handler even catches
/// <c>InvalidTextRepresentation</c> as a last line of defence. Routes,
/// authorization wiring and output-cache behaviour stay in
/// <c>Soulsjwa.ApiTests.ObjectivesEndpointTests</c>.
/// </summary>
public class ObjectiveCatalogTests : IntegrationTestBase
{
    /// <summary>Dark Souls: Remastered — seeded by the initial migration.</summary>
    private const int SeededGameId = 1;

    /// <summary>Elden Ring — seeded with several hundred predefined objectives.</summary>
    private const int ConnectorGameId = 9;

    [Theory]
    [InlineData("", 5, null, "Name")]
    [InlineData("   ", 5, null, "Name")]
    [InlineData("x", -1, null, "Score")]
    [InlineData("x", 5, "not json", "Rule")]
    [InlineData("x", 5, "[1,2,3]", "Rule")]
    public async Task CreatePredefined_WithAnInvalidField_IsRejectedAndNamesIt(
        string name, int score, string? rule, string expectedKey)
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await CreatePredefinedAsync(db, admin, name, score, rule: rule);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey(expectedKey);
    }

    [Fact]
    public async Task CreatePredefined_WithARuleOverTheByteCap_IsRejected()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var huge = "{\"==\":[1," + new string('1', ObjectiveRuleValidator.MaxRuleBytes) + "]}";

        var result = await CreatePredefinedAsync(db, admin, "huge rule", 5, rule: huge);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey("Rule");
    }

    [Fact]
    public async Task CreatePredefined_WithMalformedMetadata_IsRejected()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await CreatePredefinedAsync(db, admin, "bad metadata", 5, metadata: "{");

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey("Metadata");
    }

    [Fact]
    public async Task CreatePredefined_WithAValidRuleAndMetadata_PersistsBothAsJsonb()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        const string rule = """{"==":[1,1]}""";
        const string metadata = """{"area":"Undead Burg"}""";

        var result = await CreatePredefinedAsync(db, admin, "valid", 5, rule: rule, metadata: metadata);

        result.Status().Should().Be(StatusCodes.Status201Created);
        var stored = await CreateDbContext().Objectives
            .SingleAsync(o => o.IsPredefined && o.Name == "valid" && o.GameId == SeededGameId);
        stored.Rule.Should().NotBeNull();
        stored.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task CreatePredefined_ForAnUnknownGame_IsNotFound()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await ObjectivesEndpoint.CreatePredefined(
            new CreatePredefinedObjectiveRequest(4242, "orphan", 5, null, null, null),
            admin.Principal(), db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ListPredefined_FilteredByGame_ReturnsOnlyThatGames()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var name = $"filter-probe-{Guid.NewGuid():N}";
        await CreatePredefinedAsync(db, admin, name, 10);

        var matching = await ObjectivesEndpoint.ListPredefined(CreateDbContext(), SeededGameId, default);
        var other = await ObjectivesEndpoint.ListPredefined(CreateDbContext(), 2, default);

        matching.Value<List<PredefinedObjectiveResponse>>()
            .Should().Contain(o => o.Name == name && o.GameId == SeededGameId);
        other.Value<List<PredefinedObjectiveResponse>>()
            .Should().NotContain(o => o.Name == name);
    }

    [Fact]
    public async Task CreateObjective_AsTheOwner_PersistsItUnderThatGame()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await ObjectivesEndpoint.CreateObjective(
            f.Event.Id, f.Game.Id, new CreateObjectiveRequest("Beat the boss", 50, null, null, null),
            f.Owner.Principal(), db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status201Created);
        (await CreateDbContext().Objectives
            .CountAsync(o => o.EventGameId == f.Game.Id && o.Name == "Beat the boss"))
            .Should().Be(1);
    }

    [Fact]
    public async Task CreateObjective_ByANonOwner_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await ObjectivesEndpoint.CreateObjective(
            f.Event.Id, f.Game.Id, new CreateObjectiveRequest("x", 1, null, null, null),
            stranger.Principal(), db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CreateObjective_WithAMalformedRule_IsRejected()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await ObjectivesEndpoint.CreateObjective(
            f.Event.Id, f.Game.Id, new CreateObjectiveRequest("bad rule", 10, null, null, "not json"),
            f.Owner.Principal(), db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey("Rule");
    }

    [Fact]
    public async Task PatchObjective_AsTheOwner_AppliesNameAndScore()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await PatchAsync(db, f, f.Owner, new PatchObjectiveRequest { Name = "renamed", Score = 10 });

        result.Status().Should().Be(StatusCodes.Status200OK);
        var updated = await CreateDbContext().Objectives.SingleAsync(o => o.Id == f.Objective.Id);
        updated.Name.Should().Be("renamed");
        updated.Score.Should().Be(10);
    }

    [Fact]
    public async Task PatchObjective_ByANonOwner_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await PatchAsync(db, f, stranger, new PatchObjectiveRequest { Name = "renamed" });

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Theory]
    [InlineData("Score")]
    [InlineData("Name")]
    [InlineData("FailRule")]
    public async Task PatchObjective_WithAnInvalidField_IsRejectedAndNamesIt(string field)
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var request = field switch
        {
            "Score" => new PatchObjectiveRequest { Score = -10 },
            "Name" => new PatchObjectiveRequest { Name = "   " },
            _ => new PatchObjectiveRequest { FailRule = "not json" },
        };

        var result = await PatchAsync(db, f, f.Owner, request);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey(field);
    }

    [Fact]
    public async Task PatchObjective_SetsThenClearsTheFailRule()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        const string failRule = """{">":[{"var":"competitorCompletions"},0]}""";

        var set = await PatchAsync(db, f, f.Owner, new PatchObjectiveRequest { FailRule = failRule });

        set.Status().Should().Be(StatusCodes.Status200OK);
        (await CreateDbContext().Objectives.SingleAsync(o => o.Id == f.Objective.Id))
            .FailRule.Should().NotBeNull();

        // An explicitly-null FailRule clears it; the request object tracks
        // "was it set at all" separately, which is the whole point of the
        // Has* flags on PatchObjectiveRequest.
        var cleared = await PatchAsync(CreateDbContext(), f, f.Owner, new PatchObjectiveRequest { FailRule = null });

        cleared.Status().Should().Be(StatusCodes.Status200OK);
        (await CreateDbContext().Objectives.SingleAsync(o => o.Id == f.Objective.Id))
            .FailRule.Should().BeNull();
    }

    [Fact]
    public async Task PatchObjective_OfAnotherGame_IsNotFound()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var other = await Fixtures.AddEventAsync(db);

        // Right objective, wrong event-game: the pair is what identifies it.
        var result = await ObjectivesEndpoint.PatchObjective(
            other.Event.Id, other.Game.Id, f.Objective.Id,
            new PatchObjectiveRequest { Name = "renamed" },
            other.Owner.Principal(), db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteObjective_AsTheOwner_RemovesIt()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await ObjectivesEndpoint.DeleteObjective(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Owner.Principal(),
            db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await CreateDbContext().Objectives.AnyAsync(o => o.Id == f.Objective.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteObjective_ByANonOwner_IsForbiddenAndKeepsIt()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await ObjectivesEndpoint.DeleteObjective(
            f.Event.Id, f.Game.Id, f.Objective.Id, stranger.Principal(),
            db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
        (await CreateDbContext().Objectives.AnyAsync(o => o.Id == f.Objective.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Import_WithoutAFilter_TakesEveryOneAndIsIdempotent()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, knownGameId: ConnectorGameId);

        var first = await ImportAsync(db, f, null);

        var firstResult = first.Value<ImportPredefinedObjectivesResult>();
        firstResult.ImportedCount.Should().BeGreaterThan(0);
        firstResult.SkippedCount.Should().Be(0);

        // Idempotent on the rule: a second import skips everything the first
        // brought in rather than duplicating it.
        var second = await ImportAsync(CreateDbContext(), f, null);

        var secondResult = second.Value<ImportPredefinedObjectivesResult>();
        secondResult.ImportedCount.Should().Be(0);
        secondResult.SkippedCount.Should().Be(firstResult.ImportedCount);
    }

    /// <summary>
    /// Elden Ring's catalog has the same boss at two locations under one
    /// display name, each tracking its own flag. Import once keyed on the
    /// name, so the second location was silently never imported; identity is
    /// the rule, as the seeder already documents.
    /// </summary>
    [Fact]
    public async Task Import_KeysOnTheRule_SoSameNamedObjectivesAtDifferentLocationsBothLand()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, knownGameId: SeededGameId);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var bellum = (await CreatePredefinedAsync(db, admin, "Black Knife Assassin - Slain", 1,
            rule: """{">":[{"var":"g1_f1"},0]}""")).Value<PredefinedObjectiveResponse>();
        var stormhill = (await CreatePredefinedAsync(db, admin, "Black Knife Assassin - Slain", 1,
            rule: """{">":[{"var":"g1_f2"},0]}""")).Value<PredefinedObjectiveResponse>();

        var first = await ImportAsync(CreateDbContext(), f, [bellum.Id, stormhill.Id]);

        first.Value<ImportPredefinedObjectivesResult>().ImportedCount.Should().Be(2);
        // jsonb normalises whitespace on the way in, so match on the flags.
        var rules = await CreateDbContext().Objectives
            .Where(o => o.EventGameId == f.Game.Id && o.IsPredefined)
            .Select(o => o.Rule!)
            .ToListAsync();
        rules.Should().HaveCount(2);
        rules.Should().ContainSingle(r => r.Contains("g1_f1")).And.ContainSingle(r => r.Contains("g1_f2"));

        // And the same two are what a re-import recognises as already present.
        var second = await ImportAsync(CreateDbContext(), f, [bellum.Id, stormhill.Id]);
        second.Value<ImportPredefinedObjectivesResult>().Should().Be(new ImportPredefinedObjectivesResult(0, 2));
    }

    [Fact]
    public async Task Import_AndAssign_AppendAfterTheExistingObjectives()
    {
        var db = CreateDbContext();
        // The fixture objective sits at SortOrder 0.
        var f = await Fixtures.AddEventAsync(db, knownGameId: SeededGameId);
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var a = (await CreatePredefinedAsync(db, admin, "A", 1, rule: """{">":[{"var":"g1_f1"},0]}""")).Value<PredefinedObjectiveResponse>();
        var b = (await CreatePredefinedAsync(db, admin, "B", 1, rule: """{">":[{"var":"g1_f2"},0]}""")).Value<PredefinedObjectiveResponse>();
        var c = (await CreatePredefinedAsync(db, admin, "C", 1, rule: """{">":[{"var":"g1_f3"},0]}""")).Value<PredefinedObjectiveResponse>();

        await ImportAsync(CreateDbContext(), f, [a.Id, b.Id]);
        await ObjectivesEndpoint.AssignPredefined(
            f.Event.Id, f.Game.Id, new AssignPredefinedObjectiveRequest(c.Id),
            f.Owner.Principal(), CreateDbContext(), Audit, Cache,
            NullLogger<ObjectivesEndpoint>.Instance, default);

        var order = await CreateDbContext().Objectives
            .Where(o => o.EventGameId == f.Game.Id)
            .OrderBy(o => o.SortOrder)
            .Select(o => new { o.Name, o.SortOrder })
            .ToListAsync();
        order.Select(o => o.Name).Should().ContainInOrder("obj", "A", "B", "C");
        order.Select(o => o.SortOrder).Should().BeEquivalentTo([0, 1, 2, 3], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Import_WithAnIdSubset_TakesOnlyThose()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, knownGameId: ConnectorGameId);
        var subset = await db.Objectives
            .Where(o => o.IsPredefined && o.EventGameId == null && o.GameId == ConnectorGameId)
            .OrderBy(o => o.Name)
            .Select(o => o.Id)
            .Take(3)
            .ToListAsync();
        subset.Should().HaveCount(3);

        var result = await ImportAsync(db, f, subset);

        var imported = result.Value<ImportPredefinedObjectivesResult>();
        imported.ImportedCount.Should().Be(3);
        imported.SkippedCount.Should().Be(0);
        (await CreateDbContext().Objectives.CountAsync(o => o.EventGameId == f.Game.Id))
            .Should().Be(4, "the three imported ones plus the fixture's own");
    }

    [Fact]
    public async Task Import_ByANonOwner_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, knownGameId: ConnectorGameId);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await ObjectivesEndpoint.ImportAllPredefined(
            f.Event.Id, f.Game.Id, null, stranger.Principal(),
            db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Import_IntoAGameNotInTheEvent_IsNotFound()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, knownGameId: ConnectorGameId);

        var result = await ObjectivesEndpoint.ImportAllPredefined(
            f.Event.Id, Guid.NewGuid(), null, f.Owner.Principal(),
            db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Import_IntoACustomGame_IsNotFound()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await ImportAsync(db, f, null);

        result.Status().Should().Be(StatusCodes.Status404NotFound,
            "a custom game has no catalogue to import from");
    }

    private Task<IResult> CreatePredefinedAsync(
        Soulsjwa.Api.Infrastructure.Data.AppDbContext db,
        User admin,
        string name,
        int score,
        string? rule = null,
        string? metadata = null) =>
        ObjectivesEndpoint.CreatePredefined(
            new CreatePredefinedObjectiveRequest(SeededGameId, name, score, null, metadata, rule),
            admin.Principal(), db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

    private Task<IResult> PatchAsync(
        Soulsjwa.Api.Infrastructure.Data.AppDbContext db,
        EventFixture fixture,
        User caller,
        PatchObjectiveRequest request) =>
        ObjectivesEndpoint.PatchObjective(
            fixture.Event.Id, fixture.Game.Id, fixture.Objective.Id, request,
            caller.Principal(), db, Audit, Cache, NullLogger<ObjectivesEndpoint>.Instance, default);

    private Task<IResult> ImportAsync(
        Soulsjwa.Api.Infrastructure.Data.AppDbContext db,
        EventFixture fixture,
        List<Guid>? objectiveIds) =>
        ObjectivesEndpoint.ImportAllPredefined(
            fixture.Event.Id, fixture.Game.Id,
            objectiveIds is null ? null : new ImportPredefinedObjectivesRequest(objectiveIds),
            fixture.Owner.Principal(), db, Audit, Cache,
            NullLogger<ObjectivesEndpoint>.Instance, default);
}
