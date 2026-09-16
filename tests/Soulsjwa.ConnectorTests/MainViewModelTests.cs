using System.IO;
using FluentAssertions;
using Soulsjwa.Connector.Services;
using Soulsjwa.Connector.ViewModels;
using Soulsjwa.Shared;

namespace Soulsjwa.ConnectorTests;

// MainViewModel constructs with real ConfigurationService + ApiService.
// ConfigurationService persists to per-user application data so we delete the
// file before/after each test to keep them isolated, just like
// ConfigurationServiceTests.
public class MainViewModelTests : IDisposable
{
    private static readonly string ConfigPath = ConfigurationService.GetDefaultConfigPath();
    private static readonly string LegacyConfigPath = ConfigurationService.GetLegacyConfigPath();

    public MainViewModelTests()
    {
        DeleteConfig();
    }

    public void Dispose()
    {
        DeleteConfig();
    }

    private static void DeleteConfig()
    {
        if (File.Exists(ConfigPath))
            File.Delete(ConfigPath);
        if (File.Exists(LegacyConfigPath))
            File.Delete(LegacyConfigPath);
    }

    private static MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            new ConfigurationService(),
            new ApiService());
    }

    private static void SetLoadedGameData(MainViewModel vm, int knownGameId, List<GameDataPoint> dataPoints)
    {
        var pointsField = typeof(MainViewModel).GetField("_gameDataPoints",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        pointsField.SetValue(vm, dataPoints);

        var loadedGameField = typeof(MainViewModel).GetField("_loadedGameDataKnownGameId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        loadedGameField.SetValue(vm, knownGameId);

        vm.StartCommand.NotifyCanExecuteChanged();
        vm.DebugReadCommand.NotifyCanExecuteChanged();
    }

    [Fact]
    public void NewViewModel_WithNoConfig_HasEmptyInputsAndDisabledCommands()
    {
        var vm = CreateViewModel();

        vm.ServerUrl.Should().BeEmpty();
        vm.ApiKey.Should().BeEmpty();
        vm.ConfigSaved.Should().BeFalse();
        vm.IsLoading.Should().BeFalse();
        vm.IsStatusError.Should().BeFalse();
        vm.StatusMessage.Should().NotBeNullOrEmpty();
        vm.Events.Should().BeEmpty();
        vm.SupportedGames.Should().BeEmpty();
        vm.SelectedEvent.Should().BeNull();
        vm.SelectedGame.Should().BeNull();
        vm.IsWatching.Should().BeFalse();

        vm.SaveConfigCommand.CanExecute(null).Should().BeFalse();
        vm.ConnectCommand.CanExecute(null).Should().BeFalse();
        vm.StartCommand.CanExecute(null).Should().BeFalse();
        vm.StopCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SettingBothInputs_EnablesConnectAndSaveConfigCommands()
    {
        var vm = CreateViewModel();

        vm.ServerUrl = "https://server.test";
        vm.ApiKey = "key";

        vm.SaveConfigCommand.CanExecute(null).Should().BeTrue();
        vm.ConnectCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void SaveConfig_TrimsValuesAndPersistsThemForNextLoad()
    {
        var vm = CreateViewModel();
        vm.ServerUrl = "  https://server.test  ";
        vm.ApiKey = "  the-key  ";

        vm.SaveConfigCommand.Execute(null);

        vm.ConfigSaved.Should().BeTrue();
        vm.IsStatusError.Should().BeFalse();
        vm.StatusMessage.Should().Contain("saved");

        // A fresh viewmodel should pick up the persisted (trimmed) values.
        var reloaded = CreateViewModel();
        reloaded.ServerUrl.Should().Be("https://server.test");
        reloaded.ApiKey.Should().Be("the-key");
        reloaded.ConfigSaved.Should().BeTrue();
    }

    [Fact]
    public void NewViewModel_WhenConfigFilePresent_LoadsItIntoState()
    {
        new ConfigurationService().Save(new ConnectorConfig
        {
            ServerUrl = "https://preloaded.test",
            ApiKey = "preloaded-key",
        });

        var vm = CreateViewModel();

        vm.ServerUrl.Should().Be("https://preloaded.test");
        vm.ApiKey.Should().Be("preloaded-key");
        vm.ConfigSaved.Should().BeTrue();
        vm.StatusMessage.Should().Contain("loaded");
    }

    [Fact]
    public void NewViewModel_WhenSavedConfigHasRejectedHttpUrl_LoadsInputsAndShowsConfigurationError()
    {
        new ConfigurationService().Save(new ConnectorConfig
        {
            ServerUrl = "http://preloaded.test",
            ApiKey = "preloaded-key",
        });

        MainViewModel? vm = null;
        Action act = () => { vm = CreateViewModel(); };

        act.Should().NotThrow();
        vm.Should().NotBeNull();
        vm!.ServerUrl.Should().Be("http://preloaded.test");
        vm.ApiKey.Should().Be("preloaded-key");
        vm.ConfigSaved.Should().BeFalse();
        vm.IsStatusError.Should().BeTrue();
        vm.StatusMessage.Should().Contain("Saved configuration is invalid");
        vm.StatusMessage.Should().Contain("HTTPS");
    }

    [Fact]
    public void SaveConfig_WhenServerUrlIsRejected_ShowsConfigurationErrorWithoutThrowing()
    {
        var vm = CreateViewModel();
        vm.ServerUrl = "http://server.test";
        vm.ApiKey = "key";

        var act = () => vm.SaveConfigCommand.Execute(null);

        act.Should().NotThrow();
        vm.ConfigSaved.Should().BeFalse();
        vm.IsStatusError.Should().BeTrue();
        vm.StatusMessage.Should().Contain("Invalid configuration");
        vm.StatusMessage.Should().Contain("HTTPS");
    }

    [Fact]
    public void TheAssemblyVersion_IsTheOneConnectorConstantsDeclares()
    {
        // The csproj derives <Version> from ConnectorConstants.cs; if that
        // wiring breaks, the zip filename and the reported version drift apart.
        var assemblyVersion = typeof(MainViewModel).Assembly.GetName().Version!;
        var declared = System.Version.Parse(ConnectorConstants.Version);

        (assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build)
            .Should().Be((declared.Major, declared.Minor, declared.Build));
    }

    [Fact]
    public void ShouldSubmit_OnlyWhenThePayloadChangedOrTheHeartbeatIsDue()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        MainViewModel.ShouldSubmit("{\"a\":1}", null, default, now).Should().BeTrue("the first read always goes up");
        MainViewModel.ShouldSubmit("{\"a\":1}", "{\"a\":1}", now, now.AddSeconds(2)).Should().BeFalse("nothing changed");
        MainViewModel.ShouldSubmit("{\"a\":2}", "{\"a\":1}", now, now.AddSeconds(2)).Should().BeTrue("the state moved");
        MainViewModel.ShouldSubmit("{\"a\":1}", "{\"a\":1}", now, now + MainViewModel.HeartbeatInterval).Should().BeTrue("heartbeat");
    }

    [Fact]
    public void IsLocalConnectorAtLeast_ReturnsTrue_WhenLocalEqualsOrExceedsRequired()
    {
        MainViewModel.IsLocalConnectorAtLeast(ConnectorConstants.Version).Should().BeTrue();
        MainViewModel.IsLocalConnectorAtLeast("0.0.1").Should().BeTrue();
    }

    [Fact]
    public void IsLocalConnectorAtLeast_ReturnsFalse_WhenLocalIsOlder()
    {
        MainViewModel.IsLocalConnectorAtLeast("999.0.0").Should().BeFalse();
    }

    [Fact]
    public void IsLocalConnectorAtLeast_ReturnsFalse_WhenRequiredIsMalformed()
    {
        MainViewModel.IsLocalConnectorAtLeast("not-a-version").Should().BeFalse();
        MainViewModel.IsLocalConnectorAtLeast("").Should().BeFalse();
    }

    /// <summary>
    /// The regression behind the connector's dedicated events route: the
    /// public events list stopped carrying games, the connector kept reading
    /// them, and every event selected offered nothing to monitor. The game
    /// list is now part of the shared contract and must populate the picker.
    /// </summary>
    [Fact]
    public void SelectingAnEvent_OffersItsConnectorSupportedGames_AndSkipsTheRest()
    {
        var vm = CreateViewModel();
        var supported = new ConnectorEventGameResponse(
            Guid.NewGuid(), GameIds.EldenRingMemory, "Elden Ring", "Elden Ring", true, ConnectorConstants.Version, true);
        var custom = new ConnectorEventGameResponse(
            Guid.NewGuid(), null, "Board game night", null, false, null, false);

        vm.SelectedEvent = new ConnectorEventResponse(
            Guid.NewGuid(), "evt", "desc", true, false, [supported, custom]);

        vm.SupportedGames.Should().ContainSingle().Which.Should().BeSameAs(supported);
        vm.IsStatusError.Should().BeFalse();
        vm.StatusMessage.Should().Contain("Select a connector-supported game");
    }

    [Fact]
    public void SelectingAnEvent_WithNoSupportedGames_SaysSo()
    {
        var vm = CreateViewModel();

        vm.SelectedEvent = new ConnectorEventResponse(
            Guid.NewGuid(), "evt", "desc", true, false,
            [new ConnectorEventGameResponse(Guid.NewGuid(), null, "Custom", null, false, null, true)]);

        vm.SupportedGames.Should().BeEmpty();
        vm.StatusMessage.Should().Contain("no connector-supported games");
    }

    [Fact]
    public void SelectingAnEvent_ThatIsNotStarted_WarnsThatSubmissionsAreRefused()
    {
        var vm = CreateViewModel();

        vm.SelectedEvent = new ConnectorEventResponse(
            Guid.NewGuid(), "evt", "desc", false, false,
            [new ConnectorEventGameResponse(Guid.NewGuid(), GameIds.DarkSouls3, "Dark Souls III", "Dark Souls III", true, null, true)]);

        vm.SupportedGames.Should().ContainSingle();
        vm.IsStatusError.Should().BeFalse();
        vm.StatusMessage.Should().Contain("not been started");
    }

    [Fact]
    public void DebugReadCommand_IsDisabled_UntilGameDataIsLoaded()
    {
        var vm = CreateViewModel();
        vm.DebugReadCommand.CanExecute(null).Should().BeFalse();

        vm.SelectedGame = new ConnectorEventGameResponse(Guid.NewGuid(), GameIds.EldenRingMemory, "Elden Ring", "Elden Ring", true, "3.1.0", true);
        vm.DebugReadCommand.CanExecute(null).Should().BeFalse(
            "no game data definitions loaded yet");
    }

    [Fact]
    public void DebugReadCommand_IsEnabled_ForMemoryGame_OnceDataIsLoaded()
    {
        // All connector-supported games (DS1R/DS2/DS3/Sekiro/Elden Ring) attach
        // to the running game via SoulMemory — Debug just needs event/game/data
        // points loaded, since there's no save file to pick.
        new ConfigurationService().Save(new ConnectorConfig
        {
            ServerUrl = "https://x",
            ApiKey = "y",
        });

        var vm = CreateViewModel();
        vm.SelectedEvent = new ConnectorEventResponse(
            Guid.NewGuid(), "evt", "desc", true, false, new List<ConnectorEventGameResponse>());
        vm.SelectedGame = new ConnectorEventGameResponse(
            Guid.NewGuid(), GameIds.EldenRingMemory, "Elden Ring",
            "Elden Ring", true, ConnectorConstants.Version, true);

        SetLoadedGameData(vm, GameIds.EldenRingMemory, new List<GameDataPoint>
        {
            new("g9_game_time_ms", "Game Time (ms)", "g9_game_time_ms", "slot_stat"),
        });

        vm.DebugReadCommand.CanExecute(null).Should().BeTrue();
        vm.StartCommand.CanExecute(null).Should().BeTrue();

        vm.DebugReadCommand.Execute(null);
        // No real game process is running in CI, so the read fails to attach —
        // that's still a successful exercise of the Debug code path.
        vm.IsStatusError.Should().BeTrue();
        vm.StatusMessage.Should().Contain("Unable to attach to the running game");
    }

    [Fact]
    public void StartCommand_IsDisabled_UntilSelectedGameDataIsLoaded()
    {
        var vm = CreateViewModel();
        vm.SelectedEvent = new ConnectorEventResponse(
            Guid.NewGuid(), "evt", "desc", true, false, new List<ConnectorEventGameResponse>());
        vm.SelectedGame = new ConnectorEventGameResponse(
            Guid.NewGuid(), GameIds.EldenRingMemory, "Elden Ring",
            "Elden Ring", true, ConnectorConstants.Version, true);

        vm.StartCommand.CanExecute(null).Should().BeFalse(
            "memory polling must not start before game data definitions are loaded for the selected game");

        SetLoadedGameData(vm, GameIds.DarkSouls3, new List<GameDataPoint>
        {
            new("g3_game_time_ms", "Game Time (ms)", "g3_game_time_ms", "slot_stat"),
        });

        vm.StartCommand.CanExecute(null).Should().BeFalse(
            "data definitions loaded for a different game must not enable memory polling");

        SetLoadedGameData(vm, GameIds.EldenRingMemory, new List<GameDataPoint>
        {
            new("g9_game_time_ms", "Game Time (ms)", "g9_game_time_ms", "slot_stat"),
        });

        vm.StartCommand.CanExecute(null).Should().BeTrue(
            "matching loaded data definitions are enough to start polling");
    }

    [Fact]
    public void ChangingSelectedGame_ClearsLoadedData()
    {
        var vm = CreateViewModel();
        vm.SelectedEvent = new ConnectorEventResponse(
            Guid.NewGuid(), "evt", "desc", true, false, new List<ConnectorEventGameResponse>());
        vm.SelectedGame = new ConnectorEventGameResponse(
            Guid.NewGuid(), GameIds.EldenRingMemory, "Elden Ring",
            "Elden Ring", true, ConnectorConstants.Version, true);
        SetLoadedGameData(vm, GameIds.EldenRingMemory, new List<GameDataPoint>
        {
            new("g9_game_time_ms", "Game Time (ms)", "g9_game_time_ms", "slot_stat"),
        });
        vm.StartCommand.CanExecute(null).Should().BeTrue();

        vm.SelectedGame = new ConnectorEventGameResponse(
            Guid.NewGuid(), GameIds.DarkSouls3, "Dark Souls III",
            "Dark Souls III", true, ConnectorConstants.Version, true);

        vm.StartCommand.CanExecute(null).Should().BeFalse(
            "changing games invalidates data definitions loaded for the previous selection");
        vm.DebugReadCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void DebugReadCommand_IsDisabled_WhileWatching()
    {
        // The poll already refreshes the loaded-data viewer every tick, so a
        // manual read would only add a second reader competing with it.
        var vm = CreateViewModel();
        vm.SelectedEvent = new ConnectorEventResponse(
            Guid.NewGuid(), "evt", "desc", true, false, new List<ConnectorEventGameResponse>());
        vm.SelectedGame = new ConnectorEventGameResponse(
            Guid.NewGuid(), GameIds.EldenRingMemory, "Elden Ring",
            "Elden Ring", true, ConnectorConstants.Version, true);
        SetLoadedGameData(vm, GameIds.EldenRingMemory, new List<GameDataPoint>
        {
            new("g9_game_time_ms", "Game Time (ms)", "g9_game_time_ms", "slot_stat"),
        });
        vm.DebugReadCommand.CanExecute(null).Should().BeTrue();

        vm.IsWatching = true;
        vm.DebugReadCommand.CanExecute(null).Should().BeFalse();

        vm.IsWatching = false;
        vm.DebugReadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ChangingSelectedGame_ClearsTheLoadedValuesAndTheSessionBaseline()
    {
        var vm = CreateViewModel();
        vm.SelectedEvent = new ConnectorEventResponse(
            Guid.NewGuid(), "evt", "desc", true, false, new List<ConnectorEventGameResponse>());
        vm.SelectedGame = new ConnectorEventGameResponse(
            Guid.NewGuid(), GameIds.EldenRingMemory, "Elden Ring",
            "Elden Ring", true, ConnectorConstants.Version, true);

        vm.GameDataValues.ArmBaseline();
        vm.GameDataValues.TryApplyPayload("""{"g9_game_time_ms":1000}""");
        vm.GameDataValues.HasBaseline.Should().BeTrue();

        vm.SelectedGame = new ConnectorEventGameResponse(
            Guid.NewGuid(), GameIds.DarkSouls3, "Dark Souls III",
            "Dark Souls III", true, ConnectorConstants.Version, true);

        vm.GameDataValues.HasBaseline.Should().BeFalse();
        vm.GameDataValues.GetValue("g9_game_time_ms").Display.Should()
            .Be(GameDataValueStore.UnknownValueDisplay,
                "values are keyed by data point id, which only means anything for the game they came from");
    }
}
