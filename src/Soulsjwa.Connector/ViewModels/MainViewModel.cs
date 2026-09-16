using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soulsjwa.Connector.Services;
using Soulsjwa.Connector.Services.Adapters;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.ViewModels;

/// <summary>
/// Drives the connector workflow: configure → connect → pick event → pick
/// connector-supported game → Start, which polls the running game's live
/// process memory (via SoulMemory) on a fixed interval and submits whenever
/// the read state changes (plus a periodic heartbeat submission, see
/// <see cref="HeartbeatInterval"/>).
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ConfigurationService _configService;
    private readonly ApiService _apiService;
    private readonly GameDataReaderFactory _readerFactory = new();

    private List<GameDataPoint> _gameDataPoints = new();
    private int? _loadedGameDataKnownGameId;
    private HashSet<int> _connectorSupportedGameIds = new();

    /// <summary>
    /// What every data point last read back, plus the snapshot taken when the
    /// session started. Every open loaded-data viewer window renders from this
    /// one instance, so they all refresh together.
    /// </summary>
    private readonly GameDataValueStore _gameDataValues = new();

    internal GameDataValueStore GameDataValues => _gameDataValues;

    /// <summary>
    /// Consecutive live-poll ticks whose payload read back entirely falsy, reset
    /// to 0 on the first non-falsy value. A successful SoulMemory attach only
    /// proves the process handle opened, not that reads return live memory —
    /// EasyAntiCheat blocks external reads on an EAC-protected launch.
    /// </summary>
    private int _consecutiveEmptyReads;

    /// <summary>Empty polls before the status escalates from "no new objectives"
    /// to a stuck-read warning. ~20s at the 2s poll interval: past the brief
    /// legitimate all-zero window after Start, short of leaving the user
    /// guessing for minutes.</summary>
    private const int StuckReadWarningThreshold = 10;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Configure server URL and API key to get started.";

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _configSaved;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectGameCommand))]
    private ConnectorEventResponse? _selectedEvent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(DebugReadCommand))]
    private ConnectorEventGameResponse? _selectedGame;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(DebugReadCommand))]
    private bool _isWatching;

    [ObservableProperty]
    private int _completedThisSession;

    [ObservableProperty]
    private int _failedThisSession;

    /// <summary>
    /// Payload of the last submission that reached the server, and when. A
    /// tick whose payload is identical skips the POST — most ticks read the
    /// same state, and the server rate limit is sized for change-driven
    /// traffic — except once per <see cref="HeartbeatInterval"/>, so a
    /// submission lost to a transient error is retried without waiting for
    /// the game state to move.
    /// </summary>
    private string? _lastSubmittedPayload;
    private DateTime _lastSubmittedAtUtc;
    internal static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A tick that arrives while the previous read+submit is still running is
    /// dropped: the timer keeps firing every <see cref="MemoryPollIntervalSeconds"/>
    /// regardless of how long a submission takes, and overlapping submissions
    /// would race each other on the status and the counters.
    /// </summary>
    private int _submissionInFlight;

    /// <summary>
    /// Whether this tick's payload needs to reach the server: yes when it
    /// differs from the last one that did, or when the heartbeat is due.
    /// </summary>
    internal static bool ShouldSubmit(string payload, string? lastSubmittedPayload, DateTime lastSubmittedAtUtc, DateTime nowUtc) =>
        lastSubmittedPayload is null
        || !string.Equals(payload, lastSubmittedPayload, StringComparison.Ordinal)
        || nowUtc - lastSubmittedAtUtc >= HeartbeatInterval;

    [ObservableProperty]
    private string? _lastDebugPayload;

    [ObservableProperty]
    private string? _serverRequiredVersion;

    /// <summary>
    /// Compared by <see cref="System.Version"/> semantics; a malformed version
    /// on either side counts as "not satisfied" so the connector errs towards
    /// refusing to run.
    /// </summary>
    internal static bool IsLocalConnectorAtLeast(string requiredVersion)
    {
        if (!System.Version.TryParse(requiredVersion, out var required)) return false;
        if (!System.Version.TryParse(ConnectorConstants.Version, out var local)) return false;
        return local >= required;
    }

    public System.Collections.ObjectModel.ObservableCollection<ConnectorEventResponse> Events { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<ConnectorEventGameResponse> SupportedGames { get; } = [];

    public MainViewModel(
        ConfigurationService configService,
        ApiService apiService)
    {
        _configService = configService;
        _apiService = apiService;

        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configService.Load();
        ServerUrl = config.ServerUrl;
        ApiKey = config.ApiKey;

        if (!string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(ApiKey))
        {
            if (TryConfigureApiService(ServerUrl, ApiKey, out var errorMessage))
            {
                ConfigSaved = true;
                StatusMessage = "Configuration loaded. Press Connect to fetch events.";
                IsStatusError = false;
            }
            else
            {
                ConfigSaved = false;
                StatusMessage = $"Saved configuration is invalid: {errorMessage} Update the server URL and save again.";
                IsStatusError = true;
            }
        }
    }

    private bool TryConfigureApiService(string serverUrl, string apiKey, out string? errorMessage)
    {
        try
        {
            _apiService.Configure(serverUrl, apiKey);
            errorMessage = null;
            return true;
        }
        catch (UriFormatException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private bool CanSaveConfig() =>
        !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(ApiKey);

    [RelayCommand(CanExecute = nameof(CanSaveConfig))]
    private void SaveConfig()
    {
        var config = _configService.Load();
        config.ServerUrl = ServerUrl.Trim();
        config.ApiKey = ApiKey.Trim();

        if (!TryConfigureApiService(config.ServerUrl, config.ApiKey, out var errorMessage))
        {
            ConfigSaved = false;
            StatusMessage = $"Invalid configuration: {errorMessage}";
            IsStatusError = true;
            return;
        }

        _configService.Save(config);

        ConfigSaved = true;
        StatusMessage = "Configuration saved successfully.";
        IsStatusError = false;
    }

    private bool CanConnect() =>
        !IsLoading
        && !string.IsNullOrWhiteSpace(ServerUrl)
        && !string.IsNullOrWhiteSpace(ApiKey);

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsLoading = true;
        IsStatusError = false;
        StatusMessage = "Connecting...";
        Events.Clear();
        SupportedGames.Clear();
        SelectedEvent = null;
        SelectedGame = null;

        try
        {
            if (!ConfigSaved)
            {
                SaveConfig();
                if (!ConfigSaved) return;
            }

            // Version handshake: game-data definitions and the submission shape
            // can change between versions, so an outdated connector could
            // mis-read memory or send incompatible payloads.
            var versionResult = await _apiService.GetRequiredVersionAsync();
            if (!versionResult.IsSuccess || versionResult.Data is null)
            {
                StatusMessage = versionResult.ErrorMessage ?? "Failed to fetch required connector version.";
                IsStatusError = true;
                return;
            }
            ServerRequiredVersion = versionResult.Data.RequiredVersion;
            if (!IsLocalConnectorAtLeast(ServerRequiredVersion))
            {
                StatusMessage = $"This connector is v{ConnectorConstants.Version} but the server requires v{ServerRequiredVersion} or newer. Please update before connecting.";
                IsStatusError = true;
                return;
            }

            var supported = await _apiService.GetSupportedGamesAsync();
            if (!supported.IsSuccess || supported.Data is null)
            {
                StatusMessage = supported.ErrorMessage ?? "Failed to load supported games.";
                IsStatusError = true;
                return;
            }
            _connectorSupportedGameIds = supported.Data.Select(g => g.Id).ToHashSet();

            // The first authenticated call of the sequence: an invalid or
            // revoked key fails here with the API-key message, so no separate
            // probe is needed (the previous one hit a Development-only route
            // and could never succeed against a production server).
            var events = await _apiService.GetEventsAsync();
            if (!events.IsSuccess || events.Data is null)
            {
                StatusMessage = events.ErrorMessage ?? "Failed to load events.";
                IsStatusError = true;
                return;
            }

            foreach (var ev in events.Data)
                Events.Add(ev);

            IsConnected = true;
            StatusMessage = Events.Count == 0
                ? "Connected. You are not a competitor in any event yet — ask the organizer to add you."
                : $"Connected. {Events.Count} event(s) available — select one to continue.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsStatusError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedEventChanged(ConnectorEventResponse? value)
    {
        SupportedGames.Clear();
        SelectedGame = null;
        ClearLoadedGameDataDefinitions();
        StopWatchingIfRunning();

        if (value is null) return;

        foreach (var game in value.Games.Where(g => g.ConnectorSupported || (g.KnownGameId.HasValue && _connectorSupportedGameIds.Contains(g.KnownGameId.Value))))
            SupportedGames.Add(game);

        if (SupportedGames.Count == 0)
            StatusMessage = "This event has no connector-supported games.";
        else if (!value.IsStarted)
            StatusMessage = "Select a connector-supported game. The event has not been started yet, so submissions will be refused until the organizer starts it.";
        else
            StatusMessage = "Select a connector-supported game for this event.";
        IsStatusError = false;
    }

    partial void OnSelectedGameChanged(ConnectorEventGameResponse? value)
    {
        _ = value;
        // Changing the selection invalidates the previous game's definitions:
        // without this a memory-backed game can start polling with stale data
        // points, and the previous game's timer would keep running.
        StopWatchingIfRunning();
        ClearLoadedGameDataDefinitions();
    }

    private bool CanSelectGame() => SelectedEvent is not null;

    [RelayCommand(CanExecute = nameof(CanSelectGame))]
    private async Task SelectGameAsync()
    {
        if (SelectedGame is null) return;
        StopWatchingIfRunning();

        if (SelectedGame.KnownGameId is null)
        {
            ClearLoadedGameDataDefinitions();
            StatusMessage = "Custom games don't have connector data definitions.";
            IsStatusError = true;
            return;
        }

        IsLoading = true;
        try
        {
            var def = await _apiService.GetGameDataPointsAsync(SelectedGame.KnownGameId.Value);
            if (!def.IsSuccess || def.Data is null)
            {
                StatusMessage = def.ErrorMessage ?? "Failed to load game data definitions.";
                IsStatusError = true;
                ClearLoadedGameDataDefinitions();
                return;
            }
            _gameDataPoints = def.Data.DataPoints;
            _loadedGameDataKnownGameId = SelectedGame.KnownGameId;
            // _gameDataPoints is a plain field, so nothing re-evaluates the
            // commands that gate on loaded definitions unless we poke them.
            StartCommand.NotifyCanExecuteChanged();
            DebugReadCommand.NotifyCanExecuteChanged();
            ViewLoadedGameDataCommand.NotifyCanExecuteChanged();
            StatusMessage = $"Loaded {_gameDataPoints.Count} data points. Start when {SelectedGame.GameName} is running.";
            IsStatusError = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanStart()
    {
        if (IsWatching || SelectedEvent is null || SelectedGame is null) return false;
        return HasLoadedGameDataDefinitionsForSelectedGame();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (SelectedEvent is null || SelectedGame is null) return;

        try
        {
            StartMemoryPolling();
            IsWatching = true;
            CompletedThisSession = 0;
            FailedThisSession = 0;
            _consecutiveEmptyReads = 0;
            _lastSubmittedPayload = null;
            // Captured by the initial sync below, so the "changed since session
            // start" baseline is what the game actually read at Start rather
            // than whatever an earlier Debug press left in the store.
            _gameDataValues.ArmBaseline();
            StatusMessage = "Started. Performing initial sync...";
            IsStatusError = false;

            // Immediate first sync so newly-imported objectives catch up to the
            // current state (also how a mid-run crash recovers).
            await SubmitCurrentStateAsync();
            StatusMessage = $"Polling {SelectedGame.GameName} live process every {MemoryPollIntervalSeconds}s. Auto-submitting on state changes.";
        }
        catch (Exception ex)
        {
            IsWatching = false;
            StopWatchingIfRunning();
            StatusMessage = $"Failed to start: {ex.Message}";
            IsStatusError = true;
        }
    }

    private bool CanStop() => IsWatching;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        StopWatchingIfRunning();
        StatusMessage = "Stopped polling live process memory.";
        IsStatusError = false;
    }

    /// <summary>
    /// Same enable rule as <see cref="CanStart"/>. Debug is deliberately
    /// unavailable while watching: the poll already refreshes the loaded-data
    /// viewer every tick, so a manual read adds nothing but a second reader
    /// competing with the poll for the same process handle.
    /// </summary>
    private bool CanDebugRead()
    {
        if (IsWatching || SelectedEvent is null || SelectedGame is null) return false;
        return HasLoadedGameDataDefinitionsForSelectedGame();
    }

    /// <summary>
    /// Reads live process memory with the same reader the live submission flow
    /// uses, but prints the payload to the status panel and stdout instead of
    /// POSTing it, so users can verify what the connector sees without
    /// affecting any event.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDebugRead))]
    private void DebugRead()
    {
        if (SelectedEvent is null || SelectedGame is null) return;
        if (SelectedGame.KnownGameId is null)
        {
            StatusMessage = "Custom games don't have connector data definitions to debug.";
            IsStatusError = true;
            return;
        }

        try
        {
            var reader = CreateReaderForSelectedGame();
            var payload = reader.Read(_gameDataPoints);
            LastDebugPayload = payload;
            if (TryReportAttachmentFailure(reader)) return;
            _gameDataValues.TryApplyPayload(payload);

            // Mirror to stdout so power users running the connector from a
            // console can grep for it without staring at the GroupBox.
            Console.WriteLine($"[debug-read {DateTime.Now:HH:mm:ss}] {payload}");

            if (IsAllEmptyPayload(payload))
            {
                StatusMessage = $"Debug read OK at {DateTime.Now:HH:mm:ss} but every value came back 0/false. " +
                    "If you are already loaded into the game, this usually means the read attached to the process " +
                    "but can't see real memory — most commonly EasyAntiCheat blocking external reads. Launch the " +
                    "game without EAC (see docs/connector/README.md) and try again. Payload below.";
                IsStatusError = true;
            }
            else
            {
                StatusMessage = $"Debug read OK at {DateTime.Now:HH:mm:ss} — source: live process memory (SoulMemory), {_gameDataPoints.Count} data point(s) requested. Payload below.";
                IsStatusError = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during debug read: {ex.Message}";
            IsStatusError = true;
        }
    }

    private IGameDataReader CreateReaderForSelectedGame()
    {
        return _readerFactory.Create(SelectedGame?.KnownGameId ?? 0);
    }

    private bool TryReportAttachmentFailure(IGameDataReader reader)
    {
        if (reader is not ISoulMemoryGameAdapter { IsAttached: false } adapter)
            return false;

        StatusMessage = $"Unable to attach to the running game: {adapter.LastRefreshError ?? "unknown error"}.";
        IsStatusError = true;
        return true;
    }

    /// <summary>
    /// True when the attach looked healthy but every requested value still came
    /// back falsy. Expected briefly at the title/character-select screen; while
    /// in-game it is the classic symptom of SoulMemory holding the process
    /// handle while EasyAntiCheat blocks the underlying memory reads (see
    /// docs/connector/README.md).
    /// </summary>
    private static bool IsAllEmptyPayload(string dataJson)
    {
        using var doc = JsonDocument.Parse(dataJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        var any = false;
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            any = true;
            if (IsTruthy(property.Value))
                return false;
        }

        // An empty object (failed attach) is TryReportAttachmentFailure's case —
        // only a non-empty, all-falsy object counts as a "stuck" read here.
        return any;
    }

    private static bool IsTruthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetDouble(out var d) && d != 0,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => !string.IsNullOrEmpty(value.GetString()),
        JsonValueKind.Null => false,
        JsonValueKind.Array or JsonValueKind.Object => value.GetRawText() is not ("[]" or "{}"),
        _ => false,
    };

    /// <summary>
    /// Checks the loaded definitions belong to the currently selected game, so
    /// stale ones from a previous selection cannot enable Start / Debug.
    /// </summary>
    private bool HasLoadedGameDataDefinitionsForSelectedGame()
    {
        return SelectedGame?.KnownGameId is int gameId
            && _loadedGameDataKnownGameId == gameId
            && _gameDataPoints.Count > 0;
    }

    private void ClearLoadedGameDataDefinitions()
    {
        _gameDataPoints = new();
        _loadedGameDataKnownGameId = null;
        // Values are keyed by data point id, which only means anything for the
        // game they were read from.
        _gameDataValues.Reset();
        StartCommand.NotifyCanExecuteChanged();
        DebugReadCommand.NotifyCanExecuteChanged();
        ViewLoadedGameDataCommand.NotifyCanExecuteChanged();
    }

    private bool CanViewLoadedGameData() => _gameDataPoints.Count > 0;

    /// <summary>
    /// Opens a modeless browser window over the currently loaded
    /// <see cref="GameDataPoint"/> definitions (name, category, description)
    /// and the value each one last read back — useful to sanity-check what the
    /// connector sees for the selected game, and, once a session is running, to
    /// watch which values move away from their state at Start.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanViewLoadedGameData))]
    private void ViewLoadedGameData()
    {
        var window = new GameDataViewerWindow(
            new GameDataViewerViewModel(_gameDataPoints, SelectedGame?.GameName ?? "Selected game", _gameDataValues))
        {
            Owner = Application.Current?.MainWindow,
        };
        window.Show();
    }

    private void StopWatchingIfRunning()
    {
        StopMemoryPolling();
        // The "changed since session start" comparison only means anything
        // within one session, so the snapshot dies with it. Clearing also
        // disarms, so a read still in flight cannot capture a baseline for a
        // session that has already ended.
        _gameDataValues.ClearBaseline();

        if (IsWatching)
        {
            IsWatching = false;
        }
    }

    /// <summary>SoulMemory's IGame surface refreshes on demand; a couple of
    /// seconds is granular enough for boss-kill detection and keeps CPU usage
    /// negligible.</summary>
    private const int MemoryPollIntervalSeconds = 2;

    private System.Threading.Timer? _memoryPollTimer;

    private void StartMemoryPolling()
    {
        StopMemoryPolling();
        _memoryPollTimer = new System.Threading.Timer(
            _ => OnMemoryPollTick(),
            state: null,
            dueTime: TimeSpan.FromSeconds(MemoryPollIntervalSeconds),
            period: TimeSpan.FromSeconds(MemoryPollIntervalSeconds));
    }

    private void StopMemoryPolling()
    {
        _memoryPollTimer?.Dispose();
        _memoryPollTimer = null;
    }

    private void OnMemoryPollTick()
    {
        if (Interlocked.CompareExchange(ref _submissionInFlight, 1, 0) != 0)
            return;

        // Marshal onto UI thread because SubmitCurrentStateAsync mutates
        // observable properties bound to the view. The memory read itself is
        // pushed off that thread again inside.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _ = RunTickAsync();
            return;
        }
        dispatcher.InvokeAsync(() => _ = RunTickAsync());
    }

    private async Task RunTickAsync()
    {
        try
        {
            await SubmitCurrentStateAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _submissionInFlight, 0);
        }
    }

    private async Task SubmitCurrentStateAsync()
    {
        if (SelectedEvent is null || SelectedGame is null) return;
        if (_gameDataPoints.Count == 0) return;
        if (SelectedGame.KnownGameId is null) return; // Custom games can't submit
        if (!IsWatching) return;

        try
        {
            var reader = CreateReaderForSelectedGame();
            var points = _gameDataPoints;
            // Off the UI thread: a full Elden Ring read touches thousands of
            // flags, and doing it on the dispatcher froze the window each tick.
            var dataJson = await Task.Run(() => reader.Read(points));
            if (TryReportAttachmentFailure(reader)) return;
            // Before the IsWatching guard: a read that genuinely saw memory is
            // worth showing even if Stop landed while it was in flight, and
            // Stop has already disarmed the baseline so it cannot be captured
            // for the session that just ended.
            _gameDataValues.TryApplyPayload(dataJson);
            if (!IsWatching) return;

            // A healthy attach does not prove reads return real memory (e.g.
            // EasyAntiCheat silently zeroing them). Every tick already
            // re-attaches via TryRefresh, so the only useful response to a
            // sustained all-empty streak is to surface it in the status.
            _consecutiveEmptyReads = IsAllEmptyPayload(dataJson) ? _consecutiveEmptyReads + 1 : 0;

            var nowUtc = DateTime.UtcNow;
            if (!ShouldSubmit(dataJson, _lastSubmittedPayload, _lastSubmittedAtUtc, nowUtc))
            {
                if (_consecutiveEmptyReads < StuckReadWarningThreshold)
                {
                    StatusMessage = $"No change since {_lastSubmittedAtUtc.ToLocalTime():HH:mm:ss} — nothing to submit.";
                    IsStatusError = false;
                }
                return;
            }

            var result = await _apiService.SubmitGameDataAsync(
                SelectedEvent.Id, SelectedGame.EventGameId, dataJson);

            // Stop may have been pressed while the read/submit was in flight; a
            // late completion must not overwrite the stopped status or mutate
            // session counters.
            if (!IsWatching) return;

            if (result.IsSuccess && result.Data is not null)
            {
                _lastSubmittedPayload = dataJson;
                _lastSubmittedAtUtc = nowUtc;
                CompletedThisSession += result.Data.CompletedCount;
                FailedThisSession += result.Data.FailedCount;
                if (result.Data.CompletedCount > 0 || result.Data.FailedCount > 0)
                {
                    StatusMessage = $"✓ Completed {result.Data.CompletedCount}, failed {result.Data.FailedCount} objective(s) " +
                        $"(session totals: {CompletedThisSession} completed, {FailedThisSession} failed).";
                    IsStatusError = false;
                }
                else if (_consecutiveEmptyReads >= StuckReadWarningThreshold)
                {
                    StatusMessage = $"Still reading all zeros after {_consecutiveEmptyReads} polls at {DateTime.Now:HH:mm:ss} — " +
                        "the connector is attached but isn't seeing real game memory. If you're in-game, this is almost " +
                        "always EasyAntiCheat blocking external reads; launch without EAC and press Stop/Start again " +
                        "(see docs/connector/README.md).";
                    IsStatusError = true;
                }
                else
                {
                    StatusMessage = $"Synced at {DateTime.Now:HH:mm:ss} — no new objectives.";
                    IsStatusError = false;
                }
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Submission failed.";
                IsStatusError = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during submission: {ex.Message}";
            IsStatusError = true;
        }
    }

    public void Dispose()
    {
        StopMemoryPolling();
    }
}
