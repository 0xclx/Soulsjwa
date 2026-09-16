using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soulsjwa.Connector.Services;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.ViewModels;

/// <summary>
/// Drives a read-only browser over the <see cref="GameDataPoint"/> definitions
/// loaded for the currently selected game — searchable, filterable by
/// category, and paginated client-side since a full catalog (e.g. Elden Ring)
/// can run into the thousands of points.
/// <para>
/// Each row also shows the value that point last read back, refreshed from the
/// shared <see cref="GameDataValueStore"/> on every Debug press and every live
/// poll tick, plus what it read when the session started and whether the two
/// have ever differed.
/// </para>
/// </summary>
public partial class GameDataViewerViewModel : ObservableObject, IDisposable
{
    private readonly List<GameDataPointRow> _allRows;
    private readonly GameDataValueStore _valueStore;

    public string GameName { get; }
    public int TotalPointCount => _allRows.Count;

    // The ComboBox binds SelectedItem to CategoryFilterOption instances, never
    // directly to a nullable enum: WPF's Selector treats "SelectedItem is null"
    // as "nothing selected" rather than "the item whose value is null is
    // selected", so re-picking the null-valued ("All categories") entry after
    // picking a real category silently failed to propagate through the binding.
    public ObservableCollection<CategoryFilterOption> AvailableCategories { get; }
    public ObservableCollection<GameDataPointRow> PageItems { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CategoryFilterOption _selectedCategoryOption;

    private GameDataCategory? SelectedCategory => SelectedCategoryOption.Value;

    /// <summary>
    /// Hides every point whose value has not moved since the monitoring session
    /// started. Only meaningful while <see cref="HasBaseline"/> is true, which
    /// is why the checkbox is disabled without one.
    /// </summary>
    [ObservableProperty]
    private bool _showOnlyChangedSinceStart;

    /// <summary>
    /// Whether a session baseline exists to compare against. Clearing it (Stop)
    /// unchecks <see cref="ShowOnlyChangedSinceStart"/> rather than leaving a
    /// filter silently active with nothing to filter on.
    /// </summary>
    [ObservableProperty]
    private bool _hasBaseline;

    [ObservableProperty]
    private int _changedSinceStartCount;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _totalMatchingCount;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalMatchingCount / (double)PageSize));

    public GameDataViewerViewModel(IReadOnlyList<GameDataPoint> points, string gameName, GameDataValueStore valueStore)
    {
        _allRows = points.Select(p => new GameDataPointRow(p)).ToList();
        _valueStore = valueStore;
        GameName = gameName;
        AvailableCategories = new ObservableCollection<CategoryFilterOption>(
            Enum.GetValues<GameDataCategory>()
                .Select(c => new CategoryFilterOption((GameDataCategory?)c))
                .Prepend(new CategoryFilterOption(null)));
        _selectedCategoryOption = AvailableCategories[0];

        _valueStore.Changed += OnValueStoreChanged;
        // Before the first filter pass, so a window opened mid-session shows the
        // values and baseline already known instead of a grid full of dashes.
        RefreshValues();
        ApplyFilter(resetPage: true);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter(resetPage: true);
    partial void OnSelectedCategoryOptionChanged(CategoryFilterOption value) => ApplyFilter(resetPage: true);
    partial void OnPageSizeChanged(int value) => ApplyFilter(resetPage: true);
    partial void OnShowOnlyChangedSinceStartChanged(bool value) => ApplyFilter(resetPage: true);

    partial void OnHasBaselineChanged(bool value)
    {
        if (!value)
            ShowOnlyChangedSinceStart = false;
    }

    private void OnValueStoreChanged(object? sender, EventArgs e) => RefreshValues();

    /// <summary>
    /// Pulls every row's value from the store. Rows are mutated in place, so
    /// with the changed-filter off this never rebuilds <see cref="PageItems"/> —
    /// the bound rows simply raise their own property changes, and only the ones
    /// that actually moved do.
    /// </summary>
    private void RefreshValues()
    {
        // Only a row whose changed-flag actually moved can change which rows the
        // filter admits, so an unremarkable tick costs nothing beyond the walk.
        var membershipMoved = false;
        foreach (var row in _allRows)
            membershipMoved |= row.Update(_valueStore.GetValue(row.Id));

        ChangedSinceStartCount = _allRows.Count(r => r.IsChangedSinceBaseline);

        // Gaining or losing a baseline switches the filter on or off wholesale.
        // Losing one also unchecks ShowOnlyChangedSinceStart, which re-applies
        // the filter through its own partial — hence the guard below.
        var hadBaseline = HasBaseline;
        HasBaseline = _valueStore.HasBaseline;
        membershipMoved |= HasBaseline != hadBaseline;

        if (ShowOnlyChangedSinceStart && membershipMoved)
            ApplyFilter(resetPage: false);
    }

    /// <summary>
    /// Re-filters/sorts the full set and slices out the current page. No
    /// <c>OnPageIndexChanged</c> partial is defined, so assigning
    /// <see cref="PageIndex"/> here does not recursively re-enter this method.
    /// </summary>
    private void ApplyFilter(bool resetPage)
    {
        if (resetPage) PageIndex = 1;

        IEnumerable<GameDataPointRow> query = _allRows;
        // Cheapest predicate first: a flag the row already carries, and the one
        // that usually discards the most rows.
        if (ShowOnlyChangedSinceStart && HasBaseline)
            query = query.Where(r => r.IsChangedSinceBaseline);
        if (SelectedCategory is { } category)
            query = query.Where(r => r.Category == category);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(r =>
                r.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Id.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query
            .OrderBy(r => r.Category)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        TotalMatchingCount = filtered.Count;
        OnPropertyChanged(nameof(TotalPages));

        // Keeps the page-preserving re-slice total: a live session re-filters
        // underneath whatever page the user is on, and landing past the last one
        // would show an empty grid with no way back.
        PageIndex = Math.Clamp(PageIndex, 1, TotalPages);

        PageItems.Clear();
        foreach (var row in filtered.Skip((PageIndex - 1) * PageSize).Take(PageSize))
            PageItems.Add(row);

        OnPropertyChanged(nameof(PageIndex));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoPrevious() => PageIndex > 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousPage()
    {
        PageIndex--;
        ApplyFilter(resetPage: false);
    }

    private bool CanGoNext() => PageIndex < TotalPages;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextPage()
    {
        PageIndex++;
        ApplyFilter(resetPage: false);
    }

    /// <summary>
    /// Drops the store subscription. The viewer is modeless and several can be
    /// open at once, so without this a closed window would keep re-filtering on
    /// every poll tick for the life of the connector.
    /// </summary>
    public void Dispose() => _valueStore.Changed -= OnValueStoreChanged;
}

/// <summary>
/// One entry in the category filter ComboBox. <see cref="Value"/> is null for
/// the "All categories" option — wrapping it in a record (rather than binding
/// the ComboBox straight to <see cref="GameDataCategory"/>?) keeps every
/// selectable item a distinct non-null object, which WPF's Selector requires
/// to reliably re-select the "All categories" entry after a real category has
/// been picked.
/// </summary>
public sealed record CategoryFilterOption(GameDataCategory? Value);
