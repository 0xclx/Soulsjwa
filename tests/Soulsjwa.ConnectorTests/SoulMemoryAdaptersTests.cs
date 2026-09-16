using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using SoulMemory;
using SoulMemory.Memory;
using Soulsjwa.Connector.Services;
using Soulsjwa.Connector.Services.Adapters;
using Soulsjwa.Shared;

namespace Soulsjwa.ConnectorTests;

/// <summary>
/// Minimal, fully controllable <see cref="IGame"/> test double — lets us
/// exercise <see cref="SoulMemoryReaderBase{TGame}"/>'s orchestration (attach
/// handling, per-data-point failure isolation, one-snapshot-per-poll) without
/// a real running game process, which is unavailable in CI.
/// </summary>
internal sealed class FakeGame : IGame
{
    public bool ShouldFailRefresh { get; set; }
    public int RefreshCallCount { get; private set; }

    public ResultErr<RefreshError> TryRefresh()
    {
        RefreshCallCount++;
        return ShouldFailRefresh
            ? Result.Err(new RefreshError(RefreshErrorReason.ProcessNotRunning, "fake: not running"))
            : Result.Ok();
    }

    public TreeBuilder GetTreeBuilder() => new();
    public bool ReadEventFlag(uint eventFlagId) => false;
    public Process? GetProcess() => null;
    public int GetInGameTimeMilliseconds() => 0;
}

internal sealed class RecordingAdapter : SoulMemoryReaderBase<FakeGame>
{
    public int PrepareSnapshotCallCount { get; private set; }
    public bool ThrowOnPrepareSnapshot { get; set; }
    public List<string> ReadAttempts { get; } = [];

    public RecordingAdapter(FakeGame game) : base(game)
    {
    }

    protected override void PrepareSnapshot()
    {
        PrepareSnapshotCallCount++;
        if (ThrowOnPrepareSnapshot) throw new InvalidOperationException("snapshot failed");
    }

    protected override bool TryReadValue(GameDataPoint dataPoint, out object value)
    {
        ReadAttempts.Add(dataPoint.Id);
        value = 0;
        switch (dataPoint.Id)
        {
            case "throws":
                throw new InvalidOperationException("boom");
            case "skip":
                return false;
            default:
                value = 42;
                return true;
        }
    }
}

public class SoulMemoryReaderBaseTests
{
    private static GameDataPoint Dp(string id) => new(id, id, "0", "x");

    [Fact]
    public void Read_ReturnsEmptyPayload_AndRecordsAttachFailure_WhenTryRefreshFails()
    {
        var game = new FakeGame { ShouldFailRefresh = true };
        var adapter = new RecordingAdapter(game);

        var payload = adapter.Read([Dp("a")]);

        payload.Should().Be("{}");
        adapter.IsAttached.Should().BeFalse();
        adapter.LastRefreshError.Should().NotBeNullOrEmpty();
        // Snapshot/per-data-point reads must never run when attach fails.
        adapter.PrepareSnapshotCallCount.Should().Be(0);
        adapter.ReadAttempts.Should().BeEmpty();
    }

    [Fact]
    public void Read_SetsIsAttachedTrue_AndClearsLastRefreshError_OnSuccess()
    {
        var game = new FakeGame();
        var adapter = new RecordingAdapter(game);

        adapter.Read([Dp("a")]);

        adapter.IsAttached.Should().BeTrue();
        adapter.LastRefreshError.Should().BeNull();
    }

    [Fact]
    public void Read_CallsPrepareSnapshotExactlyOnce_RegardlessOfDataPointCount()
    {
        var game = new FakeGame();
        var adapter = new RecordingAdapter(game);
        var dataPoints = Enumerable.Range(0, 10).Select(i => Dp($"id{i}")).ToList();

        adapter.Read(dataPoints);

        adapter.PrepareSnapshotCallCount.Should().Be(1);
    }

    [Fact]
    public void Read_IsolatesPerDataPointFailures_AndAttemptsEveryDataPoint()
    {
        var game = new FakeGame();
        var adapter = new RecordingAdapter(game);
        var dataPoints = new List<GameDataPoint> { Dp("ok1"), Dp("throws"), Dp("skip"), Dp("ok2") };

        var json = adapter.Read(dataPoints);

        // Every data point is attempted, in order, even though one throws.
        adapter.ReadAttempts.Should().Equal("ok1", "throws", "skip", "ok2");

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("ok1", out var ok1).Should().BeTrue();
        ok1.GetInt64().Should().Be(42);
        doc.RootElement.TryGetProperty("ok2", out _).Should().BeTrue();
        // A throwing read and a "not supported" (false) read are both omitted,
        // not present with a bogus value.
        doc.RootElement.TryGetProperty("throws", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("skip", out _).Should().BeFalse();
    }

    [Fact]
    public void Read_IsolatesSnapshotFailure_AndStillAttemptsEveryDataPoint()
    {
        var game = new FakeGame();
        var adapter = new RecordingAdapter(game) { ThrowOnPrepareSnapshot = true };

        var json = adapter.Read([Dp("ok")]);

        adapter.ReadAttempts.Should().Equal("ok");
        JsonDocument.Parse(json).RootElement.GetProperty("ok").GetInt64().Should().Be(42);
    }

    [Fact]
    public void Read_CallsTryRefresh_OnEveryInvocation()
    {
        // Process restart attachment behavior: every poll re-attempts attach,
        // so relaunching the game between submissions recovers automatically.
        var game = new FakeGame();
        var adapter = new RecordingAdapter(game);

        adapter.Read([Dp("a")]);
        adapter.Read([Dp("a")]);
        adapter.Read([Dp("a")]);

        game.RefreshCallCount.Should().Be(3);
    }
}

public class SourceIdParsingTests
{
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("123", 123L)]
    [InlineData("11010902", 11010902L)]
    [InlineData("0x10", 16L)]
    [InlineData("0xE4E1C50", 0xE4E1C50L)]
    public void TryParseLong_AcceptsDecimalAndHex(string text, long expected)
    {
        SourceIdParsing.TryParseLong(text, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("0xZZ")]
    public void TryParseLong_RejectsGarbage(string text)
    {
        SourceIdParsing.TryParseLong(text, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParseUInt32_RejectsNegativeAndOutOfRangeValues()
    {
        SourceIdParsing.TryParseUInt32("-1", out _).Should().BeFalse();
        SourceIdParsing.TryParseUInt32("99999999999", out _).Should().BeFalse();
        SourceIdParsing.TryParseUInt32("16", out var value).Should().BeTrue();
        value.Should().Be(16u);
    }
}

public class SoulMemoryAdaptersTests
{
    private static GameDataPoint CapabilityPoint(
        string id,
        string sourceId,
        GameDataReaderCapability capability) =>
        new(id, id, sourceId, "test", ReaderCapability: capability, SourceId: sourceId);

    private static long ReadValue(JsonElement payload, string id) => payload.GetProperty(id).GetInt64();

    [Fact]
    public void GameDataReaderFactory_RoutesEachSoulMemoryGame_ToItsOwnDedicatedAdapterType()
    {
        var factory = new GameDataReaderFactory();

        // Each game must be routed to its own dedicated adapter class — not a
        // shared reader distinguishing games via a switch on data type.
        factory.Create(GameIds.DarkSouls1Remastered).Should().BeOfType<DarkSouls1Adapter>();
        factory.Create(GameIds.DarkSouls2Scholar).Should().BeOfType<DarkSouls2Adapter>();
        factory.Create(GameIds.DarkSouls3).Should().BeOfType<DarkSouls3Adapter>();
        factory.Create(GameIds.Sekiro).Should().BeOfType<SekiroAdapter>();
        factory.Create(GameIds.EldenRingMemory).Should().BeOfType<EldenRingMemoryAdapter>();

        factory.Create(GameIds.DarkSouls1Remastered).Should()
            .BeSameAs(factory.Create(GameIds.DarkSouls1Remastered),
                "the same adapter should retain attachment state across polling reads");

        // Unknown / custom games have no adapter.
        var act = () => factory.Create(999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GameDataReaderFactory_AllSoulMemoryAdapters_ImplementCapabilityInterface()
    {
        var factory = new GameDataReaderFactory();

        foreach (var gameId in new[]
                 {
                     GameIds.DarkSouls1Remastered, GameIds.DarkSouls2Scholar, GameIds.DarkSouls3,
                     GameIds.Sekiro, GameIds.EldenRingMemory,
                 })
        {
            factory.Create(gameId).Should().BeAssignableTo<ISoulMemoryGameAdapter>(
                $"game {gameId} should be routed to a dedicated ISoulMemoryGameAdapter");
        }
    }

    [Fact]
    public void DarkSouls1Adapter_RoutesEveryAdvertisedCapability()
    {
        var adapter = new DarkSouls1Adapter(
            new FakeGame(),
            flag => flag == 16,
            attribute => attribute == SoulMemory.DarkSouls1.Attribute.Strength ? 20 : -1,
            () => 123,
            () => 2,
            () => 900,
            () => new Dictionary<(SoulMemory.DarkSouls1.ItemCategory, int), int>
            {
                [(SoulMemory.DarkSouls1.ItemCategory.Key, 10)] = 3,
            },
            bonfire => bonfire == SoulMemory.DarkSouls1.Bonfire.FirelinkShrine
                ? SoulMemory.DarkSouls1.BonfireState.Kindled1
                : SoulMemory.DarkSouls1.BonfireState.Unknown,
            () => true,
            () => true,
            () => false,
            () => 5,
            () => new Vector3f(1.5f, 2.5f, 3.5f));

        using var json = JsonDocument.Parse(adapter.Read([
            CapabilityPoint("flag", "16", GameDataReaderCapability.MemoryEventFlag),
            CapabilityPoint("attribute", nameof(SoulMemory.DarkSouls1.Attribute.Strength), GameDataReaderCapability.MemoryAttribute),
            CapabilityPoint("time", "time", GameDataReaderCapability.MemoryInGameTimeMilliseconds),
            CapabilityPoint("ng", "ng", GameDataReaderCapability.MemoryNgCount),
            CapabilityPoint("health", "health", GameDataReaderCapability.MemoryPlayerHealth),
            CapabilityPoint("item", "Key:10", GameDataReaderCapability.MemoryInventoryItemQuantity),
            CapabilityPoint("bonfire", nameof(SoulMemory.DarkSouls1.Bonfire.FirelinkShrine), GameDataReaderCapability.MemoryLocationState),
            CapabilityPoint("loaded", nameof(MemoryBooleanStateSource.PlayerLoaded), GameDataReaderCapability.MemoryBooleanState),
            CapabilityPoint("credits", nameof(MemoryBooleanStateSource.CreditsRolling), GameDataReaderCapability.MemoryBooleanState),
            CapabilityPoint("slot", nameof(MemoryIntegerStateSource.CurrentSaveSlot), GameDataReaderCapability.MemoryIntegerState),
            CapabilityPoint("x", nameof(MemoryPositionComponentSource.X), GameDataReaderCapability.MemoryPositionComponent),
        ]));

        ReadValue(json.RootElement, "flag").Should().Be(1);
        ReadValue(json.RootElement, "attribute").Should().Be(20);
        ReadValue(json.RootElement, "time").Should().Be(123);
        ReadValue(json.RootElement, "ng").Should().Be(2);
        ReadValue(json.RootElement, "health").Should().Be(900);
        ReadValue(json.RootElement, "item").Should().Be(3);
        ReadValue(json.RootElement, "bonfire").Should().Be(20);
        ReadValue(json.RootElement, "loaded").Should().Be(1);
        ReadValue(json.RootElement, "credits").Should().Be(1);
        ReadValue(json.RootElement, "slot").Should().Be(5);
        json.RootElement.GetProperty("x").GetDouble().Should().Be(1.5);
    }

    [Fact]
    public void DarkSouls2Adapter_RoutesEveryAdvertisedCapability()
    {
        var adapter = new DarkSouls2Adapter(
            new FakeGame(),
            boss => (int)boss + 1,
            attribute => attribute == SoulMemory.DarkSouls2.Attribute.Vigor ? 30 : -1,
            () => true,
            () => new Vector3f(4.5f, 5.5f, 6.5f));

        using var json = JsonDocument.Parse(adapter.Read([
            CapabilityPoint("boss", "124", GameDataReaderCapability.MemoryBossKillCount),
            CapabilityPoint("attribute", nameof(SoulMemory.DarkSouls2.Attribute.Vigor), GameDataReaderCapability.MemoryAttribute),
            CapabilityPoint("loading", nameof(MemoryBooleanStateSource.Loading), GameDataReaderCapability.MemoryBooleanState),
            CapabilityPoint("y", nameof(MemoryPositionComponentSource.Y), GameDataReaderCapability.MemoryPositionComponent),
        ]));

        ReadValue(json.RootElement, "boss").Should().Be(125);
        ReadValue(json.RootElement, "attribute").Should().Be(30);
        ReadValue(json.RootElement, "loading").Should().Be(1);
        json.RootElement.GetProperty("y").GetDouble().Should().Be(5.5);
    }

    [Fact]
    public void DarkSouls3Adapter_RoutesEveryAdvertisedCapability()
    {
        var adapter = new DarkSouls3Adapter(
            new FakeGame(),
            flag => flag == 14000800,
            () => 456,
            attribute => attribute == SoulMemory.DarkSouls3.Attribute.Vigor ? 40 : -1,
            () => true,
            () => true,
            () => false,
            () => new Vector3f(7.5f, 8.5f, 9.5f));

        using var json = JsonDocument.Parse(adapter.Read([
            CapabilityPoint("flag", "14000800", GameDataReaderCapability.MemoryEventFlag),
            CapabilityPoint("time", "time", GameDataReaderCapability.MemoryInGameTimeMilliseconds),
            CapabilityPoint("attribute", nameof(SoulMemory.DarkSouls3.Attribute.Vigor), GameDataReaderCapability.MemoryAttribute),
            CapabilityPoint("loading", nameof(MemoryBooleanStateSource.Loading), GameDataReaderCapability.MemoryBooleanState),
            CapabilityPoint("loaded", nameof(MemoryBooleanStateSource.PlayerLoaded), GameDataReaderCapability.MemoryBooleanState),
            CapabilityPoint("z", nameof(MemoryPositionComponentSource.Z), GameDataReaderCapability.MemoryPositionComponent),
        ]));

        ReadValue(json.RootElement, "flag").Should().Be(1);
        ReadValue(json.RootElement, "time").Should().Be(456);
        ReadValue(json.RootElement, "attribute").Should().Be(40);
        ReadValue(json.RootElement, "loading").Should().Be(1);
        ReadValue(json.RootElement, "loaded").Should().Be(1);
        json.RootElement.GetProperty("z").GetDouble().Should().Be(9.5);
    }

    [Fact]
    public void SekiroAdapter_RoutesEveryAdvertisedCapability()
    {
        var adapter = new SekiroAdapter(
            new FakeGame(),
            flag => flag == 9303,
            () => 789,
            attribute => attribute == SoulMemory.Sekiro.Attribute.AttackPower ? 50 : -1,
            () => true,
            () => false,
            () => true,
            () => new Vector3f(10.5f, 11.5f, 12.5f));

        using var json = JsonDocument.Parse(adapter.Read([
            CapabilityPoint("flag", "9303", GameDataReaderCapability.MemoryEventFlag),
            CapabilityPoint("time", "time", GameDataReaderCapability.MemoryInGameTimeMilliseconds),
            CapabilityPoint("attribute", nameof(SoulMemory.Sekiro.Attribute.AttackPower), GameDataReaderCapability.MemoryAttribute),
            CapabilityPoint("loaded", nameof(MemoryBooleanStateSource.PlayerLoaded), GameDataReaderCapability.MemoryBooleanState),
            CapabilityPoint("bitblt", nameof(MemoryBooleanStateSource.BitBlt), GameDataReaderCapability.MemoryBooleanState),
            CapabilityPoint("x", nameof(MemoryPositionComponentSource.X), GameDataReaderCapability.MemoryPositionComponent),
        ]));

        ReadValue(json.RootElement, "flag").Should().Be(1);
        ReadValue(json.RootElement, "time").Should().Be(789);
        ReadValue(json.RootElement, "attribute").Should().Be(50);
        ReadValue(json.RootElement, "loaded").Should().Be(1);
        ReadValue(json.RootElement, "bitblt").Should().Be(1);
        json.RootElement.GetProperty("x").GetDouble().Should().Be(10.5);
    }

    [Fact]
    public void PositionSnapshots_AreCleared_WhenAReadFails()
    {
        var ds2Reads = 0;
        var ds2 = new DarkSouls2Adapter(
            new FakeGame(),
            _ => 0,
            _ => 0,
            readPosition: () => ++ds2Reads == 1 ? new Vector3f(1, 2, 3) : throw new InvalidOperationException());

        var ds3Reads = 0;
        var ds3 = new DarkSouls3Adapter(
            new FakeGame(),
            _ => false,
            () => 0,
            _ => 0,
            readPosition: () => ++ds3Reads == 1 ? new Vector3f(1, 2, 3) : throw new InvalidOperationException());

        var sekiroReads = 0;
        var sekiro = new SekiroAdapter(
            new FakeGame(),
            _ => false,
            () => 0,
            _ => 0,
            readPosition: () => ++sekiroReads == 1 ? new Vector3f(1, 2, 3) : throw new InvalidOperationException());

        var positionPoint = new[]
        {
            CapabilityPoint("x", nameof(MemoryPositionComponentSource.X), GameDataReaderCapability.MemoryPositionComponent),
        };

        foreach (var adapter in new ISoulMemoryGameAdapter[] { ds2, ds3, sekiro })
        {
            using var first = JsonDocument.Parse(adapter.Read(positionPoint));
            first.RootElement.GetProperty("x").GetDouble().Should().Be(1);

            using var second = JsonDocument.Parse(adapter.Read(positionPoint));
            second.RootElement.TryGetProperty("x", out _).Should().BeFalse();
        }
    }

    [Fact]
    public void EldenRingMemoryAdapter_RoutesEveryAdvertisedCapability()
    {
        var adapter = new EldenRingMemoryAdapter(
            new FakeGame(),
            flag => flag == 15000800,
            () => 1011,
            () => 4,
            () =>
            [
                new SoulMemory.EldenRing.Item
                {
                    Category = SoulMemory.EldenRing.Category.Goods,
                    Id = 100,
                    Name = "Test Item",
                    GroupName = "Key Items",
                },
            ],
            () => true,
            () => false,
            () => SoulMemory.EldenRing.ScreenState.InGame,
            () => new SoulMemory.EldenRing.Position
            {
                X = 13.5f,
                Y = 14.5f,
                Z = 15.5f,
                Area = 60,
                Block = 1,
                Region = 2,
                Size = 0,
            });

        using var json = JsonDocument.Parse(adapter.Read([
            CapabilityPoint("flag", "15000800", GameDataReaderCapability.MemoryEventFlag),
            CapabilityPoint("time", "time", GameDataReaderCapability.MemoryInGameTimeMilliseconds),
            CapabilityPoint("ng", "ng", GameDataReaderCapability.MemoryNgLevel),
            CapabilityPoint("item", "Goods:100", GameDataReaderCapability.MemoryEldenRingInventoryItemPresence),
            CapabilityPoint("loaded", nameof(MemoryBooleanStateSource.PlayerLoaded), GameDataReaderCapability.MemoryBooleanState),
            CapabilityPoint("screen", nameof(MemoryIntegerStateSource.ScreenState), GameDataReaderCapability.MemoryIntegerState),
            CapabilityPoint("x", nameof(MemoryPositionComponentSource.X), GameDataReaderCapability.MemoryPositionComponent),
            CapabilityPoint("area", nameof(MemoryPositionComponentSource.Area), GameDataReaderCapability.MemoryPositionComponent),
        ]));

        ReadValue(json.RootElement, "flag").Should().Be(1);
        ReadValue(json.RootElement, "time").Should().Be(1011);
        ReadValue(json.RootElement, "ng").Should().Be(4);
        ReadValue(json.RootElement, "item").Should().Be(1);
        ReadValue(json.RootElement, "loaded").Should().Be(1);
        ReadValue(json.RootElement, "screen").Should().Be(0);
        json.RootElement.GetProperty("x").GetDouble().Should().Be(13.5);
        ReadValue(json.RootElement, "area").Should().Be(60);
    }

    // No process is running in CI, so TryRefresh() reliably fails — this
    // exercises every concrete adapter's attach-failure path without a live game.

    [Fact]
    public void DarkSouls1Adapter_ReturnsEmptyPayload_WhenGameNotRunning()
    {
        var adapter = new DarkSouls1Adapter(new SoulMemory.DarkSouls1.Remastered());
        var payload = adapter.Read(new List<GameDataPoint>
        {
            new("g1_f16", "Asylum Demon", "16", "event_flag"),
            new("g1_game_time_ms", "Game Time", "g1_game_time_ms", "slot_stat"),
        });
        payload.Should().Be("{}");
        adapter.IsAttached.Should().BeFalse();
        adapter.LastRefreshError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DarkSouls2Adapter_ReturnsEmptyPayload_WhenGameNotRunning()
    {
        var adapter = new DarkSouls2Adapter(new SoulMemory.DarkSouls2.DarkSouls2());
        var payload = adapter.Read(new List<GameDataPoint>
        {
            new("g2_f124", "The Last Giant", "124", "boss_kill_count"),
        });
        payload.Should().Be("{}");
        adapter.IsAttached.Should().BeFalse();
        adapter.LastRefreshError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DarkSouls3Adapter_ReturnsEmptyPayload_WhenGameNotRunning()
    {
        var adapter = new DarkSouls3Adapter(new SoulMemory.DarkSouls3.DarkSouls3());
        var payload = adapter.Read(new List<GameDataPoint>
        {
            new("g3_f14000800", "Iudex Gundyr", "14000800", "event_flag"),
        });
        payload.Should().Be("{}");
        adapter.IsAttached.Should().BeFalse();
        adapter.LastRefreshError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SekiroAdapter_ReturnsEmptyPayload_WhenGameNotRunning()
    {
        var adapter = new SekiroAdapter(new SoulMemory.Sekiro.Sekiro());
        var payload = adapter.Read(new List<GameDataPoint>
        {
            new("g6_f9303", "Genichiro Ashina", "9303", "event_flag"),
        });
        payload.Should().Be("{}");
        adapter.IsAttached.Should().BeFalse();
        adapter.LastRefreshError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EldenRingMemoryAdapter_ReturnsEmptyPayload_WhenGameNotRunning()
    {
        var adapter = new EldenRingMemoryAdapter(new SoulMemory.EldenRing.EldenRing());
        var payload = adapter.Read(new List<GameDataPoint>
        {
            new("g9_f15000800", "Malenia, Blade of Miquella", "15000800", "event_flag"),
            new("g9_ng_level", "New Game Cycle", "ng_level", "ng_level"),
        });
        payload.Should().Be("{}");
        adapter.IsAttached.Should().BeFalse();
        adapter.LastRefreshError.Should().NotBeNullOrEmpty();
    }
}
