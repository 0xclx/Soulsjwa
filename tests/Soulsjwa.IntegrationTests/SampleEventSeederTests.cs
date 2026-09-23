using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Admin.SampleEvents;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The sample-event seeder against real Postgres: the five catalog events in
/// their states, owned by the caller, with nothing per-competitor, in one save.
/// </summary>
public class SampleEventSeederTests : IntegrationTestBase
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private async Task<(User Admin, IReadOnlyList<SampleEventSeeder.CreatedEvent> Created)> SeedAsync()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var created = await SampleEventSeeder.CreateAsync(CreateDbContext(), new AuditService(), admin.Id, Now, default);
        return (admin, created);
    }

    [Fact]
    public async Task CreatesTheCatalogEventsInOrder_OwnedByTheCaller()
    {
        var (admin, created) = await SeedAsync();

        created.Select(c => c.Name).Should().Equal(SampleEventCatalog.Build().Select(e => e.Name));
        var events = await CreateDbContext().Events.Where(e => created.Select(c => c.Id).Contains(e.Id)).ToListAsync();
        events.Should().HaveCount(5).And.OnlyContain(e =>
            e.CreatedById == admin.Id && !e.IsFeatured && !e.IsArchived && e.UrlAlias == null);
    }

    [Fact]
    public async Task SetsEachEventsLifecycleState()
    {
        var (_, created) = await SeedAsync();
        var db = CreateDbContext();

        foreach (var (definition, (id, _)) in SampleEventCatalog.Build().Zip(created))
        {
            var ev = await db.Events.Include(e => e.EventGames).ThenInclude(g => g.Objectives).SingleAsync(e => e.Id == id);
            ev.IsStarted.Should().Be(definition.StartedAgo is not null, definition.Name);
            ev.StartedAt.Should().Be(definition.StartedAgo is { } ago ? Now - ago : null, definition.Name);
            ev.TieBreakMode.Should().Be(definition.TieBreakMode, definition.Name);

            var games = ev.EventGames.OrderBy(g => g.SortOrder).ToList();
            games.Select(g => g.CustomGameName).Should().Equal(definition.Games.Select(g => g.Name), definition.Name);
            games.Should().OnlyContain(g => g.KnownGameId == null);
            games.Select((g, i) => g.IsEnabled ? i : -1).Where(i => i >= 0)
                .Should().Equal(definition.EnabledGameIndex is { } index ? [index] : Array.Empty<int>(), definition.Name);
            foreach (var (game, gameDefinition) in games.Zip(definition.Games))
            {
                game.Objectives.OrderBy(o => o.SortOrder).Select(o => (o.Name, o.Score, o.Category))
                    .Should().Equal(gameDefinition.Objectives.Select(o => (o.Name, o.Score, (string?)o.Category)));
            }
        }
    }

    [Fact]
    public async Task WritesTheRulesTextOnlyWhereTheCatalogHasIt()
    {
        var (_, created) = await SeedAsync();

        var rules = await CreateDbContext().EventRules
            .Where(r => created.Select(c => c.Id).Contains(r.EventId))
            .ToListAsync();
        rules.Should().ContainSingle().Which.EventId.Should().Be(created[1].Id);
        rules[0].Content.Should().Be(SampleEventCatalog.Build()[1].RulesMarkdown);
    }

    [Fact]
    public async Task AddsNothingPerCompetitor()
    {
        var (_, created) = await SeedAsync();
        var ids = created.Select(c => c.Id).ToList();
        var db = CreateDbContext();

        (await db.EventCompetitors.CountAsync(c => ids.Contains(c.EventId))).Should().Be(0);
        (await db.CompletedObjectives.CountAsync(c => ids.Contains(c.Objective.EventGame!.EventId))).Should().Be(0);
        (await db.TrialRuns.CountAsync(t => ids.Contains(t.EventId))).Should().Be(0);
        (await db.PlannedRuns.CountAsync(p => ids.Contains(p.EventId))).Should().Be(0);
        (await db.CalendarEntries.CountAsync(c => ids.Contains(c.EventId))).Should().Be(0);
    }

    [Fact]
    public async Task AuditsEachEventAsCreatedByTheCaller()
    {
        var (admin, created) = await SeedAsync();

        var audits = await CreateDbContext().AuditLogs
            .Where(a => a.Type == AuditEventTypes.EventCreated && a.ActorUserId == admin.Id)
            .ToListAsync();
        audits.Select(a => a.EventId).Should().BeEquivalentTo(created.Select(c => (Guid?)c.Id));
        audits.Should().OnlyContain(a =>
            System.Text.Json.JsonDocument.Parse(a.AfterJson!, default).RootElement.GetProperty("sample").GetBoolean());
    }

    [Fact]
    public async Task EveryCallCreatesAFreshSet()
    {
        var (admin, first) = await SeedAsync();
        var second = await SampleEventSeeder.CreateAsync(CreateDbContext(), new AuditService(), admin.Id, Now, default);

        second.Select(c => c.Id).Should().NotIntersectWith(first.Select(c => c.Id));
        (await CreateDbContext().Events.CountAsync(e => e.CreatedById == admin.Id)).Should().Be(10);
    }

    [Fact]
    public async Task SavesNothingWhenTheSaveFails()
    {
        var db = CreateDbContext();
        var missingOwner = Guid.NewGuid(); // violates the CreatedBy foreign key

        var act = () => SampleEventSeeder.CreateAsync(CreateDbContext(), new AuditService(), missingOwner, Now, default);

        await act.Should().ThrowAsync<DbUpdateException>();
        (await db.Events.CountAsync(e => e.Name.StartsWith(SampleEventCatalog.NamePrefix))).Should().Be(0);
        (await db.EventGames.CountAsync()).Should().Be(0);
    }
}
