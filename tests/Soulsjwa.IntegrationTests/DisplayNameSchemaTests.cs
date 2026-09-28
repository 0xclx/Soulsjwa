using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The display-name columns on real Postgres: the migration backfills the
/// Twitch name for users that existed before it, and the override is capped
/// at 50 characters.
/// </summary>
public class DisplayNameSchemaTests : IntegrationTestBase
{
    /// <summary>The migration just before the display-name one.</summary>
    private const string PreviousMigration = "20260915142021_AddOverlayTokenSettings";

    [Fact]
    public async Task Migration_BackfillsTheTwitchNameFromTheExistingDisplayName()
    {
        var db = CreateDbContext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "TwitchId", "TwitchLogin", "DisplayName", "CreatedAt", "UpdatedAt", "Role", "IsAllowlisted")
            VALUES ({id}, 'tw_legacy', 'legacy', 'Legacy Name', now(), now(), 0, true)
            """);

        await migrator.MigrateAsync();

        var user = await CreateDbContext().Users.SingleAsync(u => u.Id == id);
        user.TwitchDisplayName.Should().Be("Legacy Name");
        user.DisplayNameOverride.Should().BeNull();
        user.DisplayName.Should().Be("Legacy Name");
    }

    [Fact]
    public async Task Columns_RoundTrip()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db, "roundtrip");
        user.SetTwitchDisplayName("SolaireOfAstora");
        user.SetDisplayNameOverride("Solaire");
        await db.SaveChangesAsync();

        var stored = await CreateDbContext().Users.SingleAsync(u => u.Id == user.Id);
        stored.TwitchDisplayName.Should().Be("SolaireOfAstora");
        stored.DisplayNameOverride.Should().Be("Solaire");
        stored.DisplayName.Should().Be("Solaire");
    }

    [Fact]
    public async Task Override_IsCappedAt50Characters()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db, "toolong");
        user.SetDisplayNameOverride(new string('x', 51));

        var save = () => db.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
    }
}
