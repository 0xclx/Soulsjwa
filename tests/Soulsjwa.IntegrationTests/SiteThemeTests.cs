using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Theme.Entities;
using Soulsjwa.Api.Features.Theme.Services;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The SiteTheme singleton row must exist on a fresh database, and its nullable
/// FK to MediaAssets must be a real, enforced foreign key.
/// </summary>
public class SiteThemeTests : IntegrationTestBase
{
    [Fact]
    public async Task FreshDatabase_HasExactlyOneSeededSiteTheme()
    {
        var db = CreateDbContext();

        var themes = await db.SiteThemes.ToListAsync();

        themes.Should().ContainSingle();
        themes[0].Id.Should().Be(1);
        themes[0].BackgroundAssetId.Should().BeNull();
        themes[0].BackgroundTreatment.Should().Be(BackgroundTreatment.None);
        themes[0].Font.Should().Be(SiteFont.SystemSansSerif);
    }

    [Fact]
    public async Task SeededTheme_PassesItsOwnValidator()
    {
        var db = CreateDbContext();

        var theme = await db.SiteThemes.SingleAsync();

        SiteThemeValidator.Validate(theme).Should().BeNull();
    }

    [Fact]
    public async Task BackgroundAssetId_PointingAtANonexistentAsset_IsRejectedByTheForeignKey()
    {
        var db = CreateDbContext();

        var theme = await db.SiteThemes.SingleAsync();
        theme.BackgroundAssetId = Guid.NewGuid();

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
