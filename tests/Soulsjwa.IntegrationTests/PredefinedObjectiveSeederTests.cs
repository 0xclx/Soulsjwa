using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Api.Features.Games.Services;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The seeder runs on every start of the api container and of the migrate
/// job, against a catalog of about a thousand definitions. What it costs is
/// therefore a property worth pinning: a fixed handful of commands, not one
/// per definition. The template database is already seeded, so a run here is
/// the steady-state "nothing to add" case unless a test deletes rows first.
/// </summary>
public class PredefinedObjectiveSeederTests : IntegrationTestBase
{
    [Fact]
    public async Task SeedingAnAlreadySeededDatabase_CostsAFixedNumberOfCommands_AndAddsNothing()
    {
        var interceptor = new CommandCountingInterceptor();
        var db = CreateDbContext(interceptor);
        var before = await db.Objectives.CountAsync(o => o.IsPredefined && o.EventGameId == null);
        interceptor.Reset();

        await PredefinedObjectiveSeeder.SeedAsync(db);

        interceptor.Count.Should().BeLessThanOrEqualTo(2,
            "one query loads the whole catalog; there is nothing to write");
        (await CreateDbContext().Objectives.CountAsync(o => o.IsPredefined && o.EventGameId == null))
            .Should().Be(before);
    }

    [Fact]
    public async Task SeedingAfterAGamesCatalogWasDeleted_RestoresItWithoutAQueryPerDefinition()
    {
        var db = CreateDbContext();
        var definitions = PredefinedObjectives.ForGame(Soulsjwa.Shared.GameIds.EldenRingMemory);
        definitions.Count.Should().BeGreaterThan(100, "the test needs a game with a large catalog");
        await db.Objectives
            .Where(o => o.IsPredefined && o.EventGameId == null && o.GameId == Soulsjwa.Shared.GameIds.EldenRingMemory)
            .ExecuteDeleteAsync();

        var interceptor = new CommandCountingInterceptor();
        await PredefinedObjectiveSeeder.SeedAsync(CreateDbContext(interceptor));

        // EF batches the inserts into a few commands; the point is that the
        // count does not grow with the number of definitions.
        interceptor.Count.Should().BeLessThan(definitions.Count / 10);
        var restored = await CreateDbContext().Objectives
            .CountAsync(o => o.IsPredefined && o.EventGameId == null && o.GameId == Soulsjwa.Shared.GameIds.EldenRingMemory);
        restored.Should().Be(definitions.Count);
    }

    [Fact]
    public async Task Seeding_MatchesStoredRulesInTheirJsonbNormalisedForm()
    {
        // A second run against rows Postgres has normalised must recognise
        // every one of them; otherwise every start would re-insert the catalog.
        var db = CreateDbContext();
        var before = await db.Objectives.CountAsync(o => o.IsPredefined && o.EventGameId == null);
        var sample = await db.Objectives.FirstAsync(o => o.IsPredefined && o.EventGameId == null);
        sample.Rule.Should().Contain(": ", "jsonb reformats the stored text, which is what the seeder has to see through");

        await PredefinedObjectiveSeeder.SeedAsync(CreateDbContext());

        (await CreateDbContext().Objectives.CountAsync(o => o.IsPredefined && o.EventGameId == null))
            .Should().Be(before);
    }
}
