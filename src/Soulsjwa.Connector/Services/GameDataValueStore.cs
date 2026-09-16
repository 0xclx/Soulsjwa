using System.Collections.Generic;
using System.Text.Json;

namespace Soulsjwa.Connector.Services;

/// <summary>
/// The connector's in-memory record of what each data point last read back, plus
/// the "status quo" snapshot taken when a monitoring session starts.
/// <para>
/// Every successful read — a Debug press or a live poll tick — is merged in here
/// by <c>MainViewModel</c>, and every open loaded-data viewer window
/// renders from it, so a window opened mid-session shows the values already
/// known and windows already open refresh in place.
/// </para>
/// <para>
/// <b>Thread affinity:</b> this type does no locking or dispatching. Mutations
/// and the <see cref="Changed"/> event happen on the calling thread, and
/// <see cref="Changed"/> handlers touch WPF-bound collections, so every
/// production caller must be on the UI thread. <c>MainViewModel</c> only calls
/// it from dispatcher-bound code (the memory read itself runs off-thread, but
/// its result is applied after the await resumes on the dispatcher).
/// </para>
/// </summary>
public sealed class GameDataValueStore
{
    /// <summary>
    /// Rendered for a data point the connector has never read back — either one
    /// the game's adapter does not support, or one no read has covered yet.
    /// </summary>
    public const string UnknownValueDisplay = "—";

    private readonly Dictionary<string, StoredValue> _current = new(StringComparer.Ordinal);

    /// <summary>
    /// Ids whose value has differed from <see cref="_baseline"/> at any point
    /// since the baseline was captured. Sticky on purpose: a value that moves
    /// and moves back is still something that happened this session, and the
    /// user would otherwise never see it if it flickered between two polls.
    /// </summary>
    private readonly HashSet<string> _changedSinceBaseline = new(StringComparer.Ordinal);

    private Dictionary<string, StoredValue>? _baseline;

    /// <summary>
    /// Set by <see cref="ArmBaseline"/>, consumed by the next payload that
    /// actually read something. Start's baseline is therefore the session's
    /// first successful read rather than whatever stale values a much earlier
    /// Debug press left behind — and a failed attach never captures one.
    /// </summary>
    private bool _baselineArmed;

    /// <summary>Raised after any mutation, so viewer windows can re-read.</summary>
    public event EventHandler? Changed;

    public bool HasBaseline => _baseline is not null;

    /// <summary>
    /// Merges one reader payload (a JSON object keyed by
    /// <see cref="Shared.GameDataPoint.Id"/>) into the current values, capturing
    /// the session baseline first if one is armed.
    /// </summary>
    /// <returns>
    /// False — leaving the store untouched — for malformed JSON, a non-object
    /// root, or an empty object. An empty object is what a failed SoulMemory
    /// attach returns, and it must neither blank every displayed value nor be
    /// mistaken for a baseline.
    /// </returns>
    public bool TryApplyPayload(string dataJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(dataJson);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            // Merge rather than replace: an adapter omits every point it cannot
            // read, so a point missing from this payload is either permanently
            // unsupported (never in _current, stays unknown) or a transient read
            // failure — neither should blank a value the user already saw.
            var applied = 0;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                _current[property.Name] = new StoredValue(property.Value.GetRawText(), FormatDisplay(property.Value));
                applied++;
            }

            // An empty object mutated nothing, so bailing out here still leaves
            // the store exactly as it was.
            if (applied == 0)
                return false;
        }

        if (_baselineArmed)
        {
            _baseline = new Dictionary<string, StoredValue>(_current, StringComparer.Ordinal);
            _baselineArmed = false;
            _changedSinceBaseline.Clear();
        }
        else if (_baseline is { } baseline)
        {
            foreach (var (id, value) in _current)
            {
                // A point absent from the baseline but readable now counts as
                // changed: it became visible during the session.
                if (!baseline.TryGetValue(id, out var before) || !string.Equals(before.RawText, value.RawText, StringComparison.Ordinal))
                    _changedSinceBaseline.Add(id);
            }
        }

        RaiseChanged();
        return true;
    }

    /// <summary>
    /// Arms the session baseline. The snapshot is taken by the next payload that
    /// reads something, which for Start is its immediate initial sync.
    /// </summary>
    public void ArmBaseline()
    {
        _baselineArmed = true;
        _baseline = null;
        _changedSinceBaseline.Clear();
        RaiseChanged();
    }

    /// <summary>
    /// Drops the session baseline (Stop, or anything that ends a session). Also
    /// disarms, so a Stop that races an in-flight read cannot let that read
    /// capture a baseline for a session that is already over. Current values
    /// survive — they are still the last thing the connector read.
    /// </summary>
    public void ClearBaseline()
    {
        _baselineArmed = false;
        _baseline = null;
        _changedSinceBaseline.Clear();
        RaiseChanged();
    }

    /// <summary>
    /// Drops everything, for when the loaded definitions themselves are no
    /// longer valid (the selected event or game changed).
    /// </summary>
    public void Reset()
    {
        _current.Clear();
        _baselineArmed = false;
        _baseline = null;
        _changedSinceBaseline.Clear();
        RaiseChanged();
    }

    /// <summary>What the viewer should render for one data point.</summary>
    public GameDataValueView GetValue(string id)
    {
        var display = _current.TryGetValue(id, out var current) ? current.Display : UnknownValueDisplay;

        var baselineDisplay = _baseline is { } baseline && baseline.TryGetValue(id, out var before)
            ? before.Display
            : UnknownValueDisplay;

        return new GameDataValueView(display, baselineDisplay, HasBaseline && _changedSinceBaseline.Contains(id));
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Renders a read value the way the debug payload and the JsonLogic rules
    /// see it — flags stay 0/1 rather than becoming No/Yes — so what the viewer
    /// shows can be compared directly against an objective's rule.
    /// </summary>
    private static string FormatDisplay(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
}

/// <summary>
/// One data point's rendered state: what it reads now, what it read when the
/// session started, and whether those have ever differed.
/// </summary>
public readonly record struct GameDataValueView(
    string Display,
    string BaselineDisplay,
    bool IsChangedSinceBaseline);

/// <summary>
/// A stored read. <see cref="RawText"/> is the value's raw JSON text and is what
/// change detection compares — exact, and immune to the formatting drift a
/// round-trip through a display string could introduce.
/// </summary>
internal readonly record struct StoredValue(string RawText, string Display);
