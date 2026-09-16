using FluentAssertions;
using Soulsjwa.Connector.Services;

namespace Soulsjwa.ConnectorTests;

public class GameDataValueStoreTests
{
    private const string PointA = "g9_a";
    private const string PointB = "g9_b";

    private static int CountChangedEvents(GameDataValueStore store, Action act)
    {
        var raised = 0;
        void Handler(object? sender, EventArgs e) => raised++;
        store.Changed += Handler;
        try
        {
            act();
        }
        finally
        {
            store.Changed -= Handler;
        }
        return raised;
    }

    [Fact]
    public void TryApplyPayload_StoresValues_AndRaisesChanged()
    {
        var store = new GameDataValueStore();

        var raised = CountChangedEvents(store, () =>
            store.TryApplyPayload($$"""{"{{PointA}}":1,"{{PointB}}":42}""").Should().BeTrue());

        raised.Should().Be(1);
        store.GetValue(PointA).Display.Should().Be("1");
        store.GetValue(PointB).Display.Should().Be("42");
    }

    [Fact]
    public void TryApplyPayload_WithEmptyObject_IsRejected_AndKeepsPreviousValues()
    {
        // {} is what a failed SoulMemory attach returns. It must not blank the
        // values the user is already looking at.
        var store = new GameDataValueStore();
        store.TryApplyPayload($$"""{"{{PointA}}":7}""");

        var raised = CountChangedEvents(store, () =>
            store.TryApplyPayload("{}").Should().BeFalse());

        raised.Should().Be(0);
        store.GetValue(PointA).Display.Should().Be("7");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("123")]
    public void TryApplyPayload_WithoutAJsonObjectRoot_IsRejected(string payload)
    {
        var store = new GameDataValueStore();

        store.TryApplyPayload(payload).Should().BeFalse();
    }

    [Fact]
    public void TryApplyPayload_MergesPartialPayloads_KeepingPreviouslyReadValues()
    {
        // Adapters omit every point they cannot read, so a point missing from
        // one payload is not a point whose value went away.
        var store = new GameDataValueStore();
        store.TryApplyPayload($$"""{"{{PointA}}":1,"{{PointB}}":2}""");

        store.TryApplyPayload($$"""{"{{PointB}}":3}""").Should().BeTrue();

        store.GetValue(PointA).Display.Should().Be("1");
        store.GetValue(PointB).Display.Should().Be("3");
    }

    [Fact]
    public void GetValue_ForAPointNeverRead_ReturnsUnknownDisplay()
    {
        var store = new GameDataValueStore();

        var view = store.GetValue(PointA);

        view.Display.Should().Be(GameDataValueStore.UnknownValueDisplay);
        view.BaselineDisplay.Should().Be(GameDataValueStore.UnknownValueDisplay);
        view.IsChangedSinceBaseline.Should().BeFalse();
    }

    [Fact]
    public void ArmBaseline_CapturesOnTheFirstSuccessfulPayload_NotAtArmTime()
    {
        // The values sitting in the store when Start is pressed can be minutes
        // old (an earlier Debug press); the session's own first read is what
        // "status quo at Start" means.
        var store = new GameDataValueStore();
        store.TryApplyPayload($$"""{"{{PointA}}":1}""");

        store.ArmBaseline();
        store.HasBaseline.Should().BeFalse("arming alone captures nothing");

        store.TryApplyPayload($$"""{"{{PointA}}":5}""");

        store.HasBaseline.Should().BeTrue();
        store.GetValue(PointA).BaselineDisplay.Should().Be("5");
        store.GetValue(PointA).IsChangedSinceBaseline.Should().BeFalse();
    }

    [Fact]
    public void ArmBaseline_ThenFailedAttachPayload_LeavesHasBaselineFalse()
    {
        var store = new GameDataValueStore();
        store.TryApplyPayload($$"""{"{{PointA}}":1}""");
        store.ArmBaseline();

        store.TryApplyPayload("{}").Should().BeFalse();

        store.HasBaseline.Should().BeFalse();
    }

    [Fact]
    public void GetValue_ReportsUnchanged_WithoutABaseline()
    {
        var store = new GameDataValueStore();
        store.TryApplyPayload($$"""{"{{PointA}}":1}""");
        store.TryApplyPayload($$"""{"{{PointA}}":2}""");

        store.GetValue(PointA).IsChangedSinceBaseline.Should().BeFalse();
    }

    [Fact]
    public void GetValue_ReportsChanged_WhenTheValueDiffersFromBaseline()
    {
        var store = new GameDataValueStore();
        store.ArmBaseline();
        store.TryApplyPayload($$"""{"{{PointA}}":0,"{{PointB}}":0}""");

        store.TryApplyPayload($$"""{"{{PointA}}":1,"{{PointB}}":0}""");

        var changed = store.GetValue(PointA);
        changed.IsChangedSinceBaseline.Should().BeTrue();
        changed.Display.Should().Be("1");
        changed.BaselineDisplay.Should().Be("0");
        store.GetValue(PointB).IsChangedSinceBaseline.Should().BeFalse();
    }

    [Fact]
    public void GetValue_StaysChanged_WhenTheValueRevertsToItsBaseline()
    {
        // Sticky on purpose: a value that flickers between two polls is still
        // something that happened this session.
        var store = new GameDataValueStore();
        store.ArmBaseline();
        store.TryApplyPayload($$"""{"{{PointA}}":0}""");
        store.TryApplyPayload($$"""{"{{PointA}}":1}""");

        store.TryApplyPayload($$"""{"{{PointA}}":0}""");

        var view = store.GetValue(PointA);
        view.IsChangedSinceBaseline.Should().BeTrue();
        view.Display.Should().Be("0");
        view.BaselineDisplay.Should().Be("0");
    }

    [Fact]
    public void GetValue_ReportsChanged_ForAPointAbsentAtBaselineThatLaterReads()
    {
        var store = new GameDataValueStore();
        store.ArmBaseline();
        store.TryApplyPayload($$"""{"{{PointA}}":1}""");

        store.TryApplyPayload($$"""{"{{PointA}}":1,"{{PointB}}":4}""");

        var view = store.GetValue(PointB);
        view.IsChangedSinceBaseline.Should().BeTrue("it became readable during the session");
        view.BaselineDisplay.Should().Be(GameDataValueStore.UnknownValueDisplay);
    }

    [Fact]
    public void GetValue_ReportsUnchanged_ForAPointNeverPresentInAnyPayload()
    {
        // A point this game's adapter does not support is not a change.
        var store = new GameDataValueStore();
        store.ArmBaseline();
        store.TryApplyPayload($$"""{"{{PointA}}":1}""");
        store.TryApplyPayload($$"""{"{{PointA}}":2}""");

        store.GetValue(PointB).IsChangedSinceBaseline.Should().BeFalse();
    }

    [Fact]
    public void TryApplyPayload_FormatsStringsWithoutQuotes_AndEverythingElseAsRawJson()
    {
        var store = new GameDataValueStore();

        store.TryApplyPayload("""{"s":"Firelink Shrine","n":1.5,"b":true}""");

        store.GetValue("s").Display.Should().Be("Firelink Shrine");
        store.GetValue("n").Display.Should().Be("1.5");
        store.GetValue("b").Display.Should().Be("true");
    }

    [Fact]
    public void ClearBaseline_DropsTheBaseline_DisarmsCapture_AndKeepsCurrentValues()
    {
        var store = new GameDataValueStore();
        store.ArmBaseline();
        store.TryApplyPayload($$"""{"{{PointA}}":1}""");
        store.TryApplyPayload($$"""{"{{PointA}}":2}""");

        var raised = CountChangedEvents(store, store.ClearBaseline);

        raised.Should().Be(1);
        store.HasBaseline.Should().BeFalse();

        // Disarmed, not merely cleared: a read that was already in flight when
        // Stop was pressed must not capture a baseline for the ended session.
        store.TryApplyPayload($$"""{"{{PointA}}":3}""");
        store.HasBaseline.Should().BeFalse();

        var view = store.GetValue(PointA);
        view.Display.Should().Be("3", "values outlive the session that read them");
        view.IsChangedSinceBaseline.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsValuesAndBaseline()
    {
        var store = new GameDataValueStore();
        store.ArmBaseline();
        store.TryApplyPayload($$"""{"{{PointA}}":1}""");
        store.TryApplyPayload($$"""{"{{PointA}}":2}""");

        var raised = CountChangedEvents(store, store.Reset);

        raised.Should().Be(1);
        store.HasBaseline.Should().BeFalse();
        store.GetValue(PointA).Display.Should().Be(GameDataValueStore.UnknownValueDisplay);
    }
}
