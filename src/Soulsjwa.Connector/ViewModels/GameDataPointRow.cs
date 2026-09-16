using CommunityToolkit.Mvvm.ComponentModel;
using Soulsjwa.Connector.Services;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.ViewModels;

/// <summary>
/// One row of the loaded-data viewer: an immutable <see cref="GameDataPoint"/>
/// definition plus the mutable state the viewer renders next to it.
/// <para>
/// Rows are built once per viewer window and mutated in place for the rest of
/// its life. Rebuilding them each poll tick would churn thousands of objects
/// every two seconds for a catalog the size of Elden Ring's, and would reset the
/// user's selection and scroll position along the way.
/// </para>
/// </summary>
public sealed partial class GameDataPointRow : ObservableObject
{
    private readonly GameDataPoint _point;

    // Pass-throughs, not copies: the ListView's GridView columns, its
    // PropertyGroupDescription on Category, and the viewer's search predicate
    // all address these names directly on the bound item.
    public string Id => _point.Id;
    public string DisplayName => _point.DisplayName;
    public string Description => _point.Description;
    public GameDataCategory Category => _point.Category;

    [ObservableProperty]
    private string _value = GameDataValueStore.UnknownValueDisplay;

    [ObservableProperty]
    private string _baselineValue = GameDataValueStore.UnknownValueDisplay;

    [ObservableProperty]
    private bool _isChangedSinceBaseline;

    public GameDataPointRow(GameDataPoint point) => _point = point;

    /// <summary>Applies the store's current view of this point.</summary>
    /// <returns>
    /// True when <see cref="IsChangedSinceBaseline"/> actually flipped — the
    /// viewer's signal that the "changed since session start" filter's
    /// membership moved and the page needs re-slicing. A tick where nothing
    /// moved returns false and raises no property change at all, because the
    /// generated setters compare before assigning.
    /// </returns>
    public bool Update(in GameDataValueView view)
    {
        Value = view.Display;
        BaselineValue = view.BaselineDisplay;

        var flipped = IsChangedSinceBaseline != view.IsChangedSinceBaseline;
        IsChangedSinceBaseline = view.IsChangedSinceBaseline;
        return flipped;
    }
}
