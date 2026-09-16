using FluentAssertions;
using Soulsjwa.Connector.Services;
using Soulsjwa.Connector.ViewModels;
using Soulsjwa.Shared;

namespace Soulsjwa.ConnectorTests;

public class GameDataViewerViewModelTests
{
    private static GameDataPoint Point(string id, string name, GameDataCategory category, string description = "") =>
        new(id, name, Offset: id, DataType: "event_flag", Description: description, Category: category);

    private static List<GameDataPoint> BuildPoints(int count, GameDataCategory category) =>
        Enumerable.Range(1, count)
            .Select(i => Point($"{category}_{i}", $"{category} Point {i:D3}", category))
            .ToList();

    private static GameDataViewerViewModel CreateViewModel(
        IReadOnlyList<GameDataPoint> points,
        string gameName = "Elden Ring",
        GameDataValueStore? valueStore = null) =>
        new(points, gameName, valueStore ?? new GameDataValueStore());

    [Fact]
    public void NewViewModel_LoadsFirstPage_SortedByCategoryThenName()
    {
        var points = new List<GameDataPoint>
        {
            Point("b1", "Bravo Boss", GameDataCategory.Bosses),
            Point("a1", "Alpha Boss", GameDataCategory.Bosses),
            Point("s1", "A Stat", GameDataCategory.Stats),
        };

        var vm = CreateViewModel(points);

        vm.TotalPointCount.Should().Be(3);
        vm.TotalMatchingCount.Should().Be(3);
        vm.PageItems.Select(p => p.DisplayName).Should().Equal("Alpha Boss", "Bravo Boss", "A Stat");
    }

    [Fact]
    public void SearchText_FiltersByNameDescriptionOrId_CaseInsensitively()
    {
        var points = new List<GameDataPoint>
        {
            Point("id_x", "Margit the Fell Omen", GameDataCategory.Bosses, "First legacy dungeon boss"),
            Point("id_y", "Godrick the Grafted", GameDataCategory.Bosses),
            Point("id_z", "Flask of Wondrous Physick", GameDataCategory.Inventory),
        };
        var vm = CreateViewModel(points);

        vm.SearchText = "margit";
        vm.PageItems.Select(p => p.Id).Should().Equal("id_x");

        vm.SearchText = "legacy dungeon";
        vm.PageItems.Select(p => p.Id).Should().Equal("id_x");

        vm.SearchText = "id_z";
        vm.PageItems.Select(p => p.Id).Should().Equal("id_z");

        vm.SearchText = "";
        vm.TotalMatchingCount.Should().Be(3);
    }

    [Fact]
    public void SelectedCategory_FiltersToThatCategoryOnly()
    {
        var points = new List<GameDataPoint>
        {
            Point("a", "A", GameDataCategory.Bosses),
            Point("b", "B", GameDataCategory.Inventory),
            Point("c", "C", GameDataCategory.Inventory),
        };
        var vm = CreateViewModel(points, "DS3");

        vm.SelectedCategoryOption = vm.AvailableCategories.Single(o => o.Value == GameDataCategory.Inventory);

        vm.TotalMatchingCount.Should().Be(2);
        vm.PageItems.Select(p => p.Id).Should().BeEquivalentTo(["b", "c"]);
    }

    [Fact]
    public void ReselectingAllCategories_AfterARealCategory_ClearsTheFilter()
    {
        var points = new List<GameDataPoint>
        {
            Point("a", "A", GameDataCategory.Bosses),
            Point("b", "B", GameDataCategory.Inventory),
        };
        var vm = CreateViewModel(points, "DS3");
        var allCategoriesOption = vm.SelectedCategoryOption;

        vm.SelectedCategoryOption = vm.AvailableCategories.Single(o => o.Value == GameDataCategory.Bosses);
        vm.TotalMatchingCount.Should().Be(1);

        vm.SelectedCategoryOption = allCategoriesOption;

        vm.TotalMatchingCount.Should().Be(2);
    }

    [Fact]
    public void ChangingFilters_ResetsToFirstPage()
    {
        var vm = CreateViewModel(BuildPoints(120, GameDataCategory.Bosses), "DS3");
        vm.PageSize = 50;

        vm.NextPageCommand.Execute(null);
        vm.PageIndex.Should().Be(2);

        vm.SearchText = "Point 0";
        vm.PageIndex.Should().Be(1);
    }

    [Fact]
    public void Pagination_SlicesResultsAndExposesTotalPages()
    {
        var vm = CreateViewModel(BuildPoints(120, GameDataCategory.Bosses), "DS3");
        vm.PageSize = 50;

        vm.TotalPages.Should().Be(3);
        vm.PageItems.Should().HaveCount(50);
        vm.PreviousPageCommand.CanExecute(null).Should().BeFalse();
        vm.NextPageCommand.CanExecute(null).Should().BeTrue();

        vm.NextPageCommand.Execute(null);
        vm.PageIndex.Should().Be(2);
        vm.PageItems.Should().HaveCount(50);

        vm.NextPageCommand.Execute(null);
        vm.PageIndex.Should().Be(3);
        vm.PageItems.Should().HaveCount(20);
        vm.NextPageCommand.CanExecute(null).Should().BeFalse();

        vm.PreviousPageCommand.Execute(null);
        vm.PageIndex.Should().Be(2);
        vm.PreviousPageCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void PageSizeChange_ResetsToFirstPageAndReslices()
    {
        var vm = CreateViewModel(BuildPoints(120, GameDataCategory.Bosses), "DS3");
        vm.PageSize = 50;
        vm.NextPageCommand.Execute(null);

        vm.PageSize = 100;

        vm.PageIndex.Should().Be(1);
        vm.TotalPages.Should().Be(2);
        vm.PageItems.Should().HaveCount(100);
    }

    [Fact]
    public void NewViewModel_ShowsValuesAlreadyKnownToTheStore()
    {
        // A viewer opened mid-session must not start out full of dashes.
        var store = new GameDataValueStore();
        store.ArmBaseline();
        store.TryApplyPayload("""{"a":0}""");
        store.TryApplyPayload("""{"a":1}""");

        var vm = CreateViewModel([Point("a", "Alpha", GameDataCategory.Bosses)], valueStore: store);

        var row = vm.PageItems.Single();
        row.Value.Should().Be("1");
        row.BaselineValue.Should().Be("0");
        row.IsChangedSinceBaseline.Should().BeTrue();
        vm.HasBaseline.Should().BeTrue();
        vm.ChangedSinceStartCount.Should().Be(1);
    }

    [Fact]
    public void StoreUpdate_MutatesRowsInPlace_WithoutRebuildingPageItems()
    {
        // Rebuilding thousands of rows every two seconds would churn the grid
        // and drop the user's selection; rows are stable for the window's life.
        var store = new GameDataValueStore();
        var vm = CreateViewModel([Point("a", "Alpha", GameDataCategory.Bosses)], valueStore: store);
        var row = vm.PageItems.Single();

        store.TryApplyPayload("""{"a":9}""");

        vm.PageItems.Single().Should().BeSameAs(row);
        row.Value.Should().Be("9");
    }

    [Fact]
    public void ChangedSinceStartFilter_ShowsOnlyRowsWhoseValueMovedSinceTheBaseline()
    {
        var store = new GameDataValueStore();
        var vm = CreateViewModel(
            [Point("a", "Alpha", GameDataCategory.Bosses), Point("b", "Bravo", GameDataCategory.Bosses)],
            valueStore: store);

        store.ArmBaseline();
        store.TryApplyPayload("""{"a":0,"b":0}""");
        vm.ShowOnlyChangedSinceStart = true;
        vm.TotalMatchingCount.Should().Be(0);

        store.TryApplyPayload("""{"a":1,"b":0}""");

        vm.PageItems.Select(r => r.Id).Should().Equal("a");
        vm.ChangedSinceStartCount.Should().Be(1);
    }

    [Fact]
    public void ChangedSinceStartFilter_KeepsARevertedRowListed()
    {
        var store = new GameDataValueStore();
        var vm = CreateViewModel([Point("a", "Alpha", GameDataCategory.Bosses)], valueStore: store);

        store.ArmBaseline();
        store.TryApplyPayload("""{"a":0}""");
        vm.ShowOnlyChangedSinceStart = true;
        store.TryApplyPayload("""{"a":1}""");
        store.TryApplyPayload("""{"a":0}""");

        vm.PageItems.Select(r => r.Id).Should().Equal("a");
    }

    [Fact]
    public void ChangedSinceStartFilter_CannotBeTurnedOn_WithoutABaseline()
    {
        var store = new GameDataValueStore();
        var vm = CreateViewModel(
            [Point("a", "Alpha", GameDataCategory.Bosses), Point("b", "Bravo", GameDataCategory.Bosses)],
            valueStore: store);
        store.TryApplyPayload("""{"a":1,"b":2}""");

        vm.HasBaseline.Should().BeFalse();
        vm.ShowOnlyChangedSinceStart = true;

        vm.TotalMatchingCount.Should().Be(2, "with nothing to compare against the filter must not hide anything");
    }

    [Fact]
    public void ChangedSinceStartFilter_StartsApplying_AsSoonAsABaselineExists()
    {
        var store = new GameDataValueStore();
        var vm = CreateViewModel(
            [Point("a", "Alpha", GameDataCategory.Bosses), Point("b", "Bravo", GameDataCategory.Bosses)],
            valueStore: store);
        vm.ShowOnlyChangedSinceStart = true;
        vm.TotalMatchingCount.Should().Be(2);

        store.ArmBaseline();
        store.TryApplyPayload("""{"a":0,"b":0}""");

        vm.HasBaseline.Should().BeTrue();
        vm.TotalMatchingCount.Should().Be(0, "nothing has moved since the baseline was taken");
    }

    [Fact]
    public void ClearingTheBaseline_UnchecksTheChangedFilter_AndRestoresAllRows()
    {
        var store = new GameDataValueStore();
        var vm = CreateViewModel(
            [Point("a", "Alpha", GameDataCategory.Bosses), Point("b", "Bravo", GameDataCategory.Bosses)],
            valueStore: store);

        store.ArmBaseline();
        store.TryApplyPayload("""{"a":0,"b":0}""");
        store.TryApplyPayload("""{"a":1,"b":0}""");
        vm.ShowOnlyChangedSinceStart = true;
        vm.TotalMatchingCount.Should().Be(1);

        store.ClearBaseline();

        vm.HasBaseline.Should().BeFalse();
        vm.ShowOnlyChangedSinceStart.Should().BeFalse();
        vm.TotalMatchingCount.Should().Be(2);
        vm.PageItems.Should().OnlyContain(r => r.BaselineValue == GameDataValueStore.UnknownValueDisplay);
    }

    [Fact]
    public void ValueTick_KeepsTheCurrentPage_WhenTheChangedFilterIsOff()
    {
        var store = new GameDataValueStore();
        var vm = CreateViewModel(BuildPoints(120, GameDataCategory.Bosses), valueStore: store);
        vm.PageSize = 50;
        vm.NextPageCommand.Execute(null);

        store.TryApplyPayload($$"""{"{{GameDataCategory.Bosses}}_1":1}""");

        vm.PageIndex.Should().Be(2);
    }

    [Fact]
    public void ArmingANewBaseline_DropsTheChangedFilter_AndReturnsToAPageThatExists()
    {
        // Stop/Start with a viewer left open: the previous session's changed set
        // is gone, so the filtered page the user was on no longer exists.
        var store = new GameDataValueStore();
        var vm = CreateViewModel(BuildPoints(120, GameDataCategory.Bosses), valueStore: store);
        vm.PageSize = 50;

        store.ArmBaseline();
        store.TryApplyPayload(BuildPayload(120, value: 0));
        store.TryApplyPayload(BuildPayload(120, value: 1));
        vm.ShowOnlyChangedSinceStart = true;
        vm.NextPageCommand.Execute(null);
        vm.PageIndex.Should().Be(2);

        store.ArmBaseline();

        vm.HasBaseline.Should().BeFalse();
        vm.ShowOnlyChangedSinceStart.Should().BeFalse();
        vm.ChangedSinceStartCount.Should().Be(0);
        vm.PageIndex.Should().Be(1);
        vm.PageItems.Should().HaveCount(50);
    }

    [Fact]
    public void Dispose_UnsubscribesFromTheStore()
    {
        var store = new GameDataValueStore();
        var vm = CreateViewModel([Point("a", "Alpha", GameDataCategory.Bosses)], valueStore: store);
        var row = vm.PageItems.Single();

        vm.Dispose();
        store.TryApplyPayload("""{"a":9}""");

        row.Value.Should().Be(GameDataValueStore.UnknownValueDisplay);
    }

    private static string BuildPayload(int count, int value) =>
        "{" + string.Join(",", Enumerable.Range(1, count)
            .Select(i => $"\"{GameDataCategory.Bosses}_{i}\":{value}")) + "}";
}
