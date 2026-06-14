using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Tests.Core;

public class NetworkCalculatorTests
{
    [Fact(Timeout = 30000)]
    public void CalculateDelta_returns_difference_from_previous()
    {
        var (rx, tx) = NetworkCalculator.CalculateDelta(
            currRx: 1500, currTx: 2500,
            prevRx: 1000, prevTx: 2000);

        Assert.Equal(500UL, rx);
        Assert.Equal(500UL, tx);
    }

    [Fact(Timeout = 30000)]
    public void CalculateDelta_clamps_negative_to_zero()
    {
        var (rx, tx) = NetworkCalculator.CalculateDelta(
            currRx: 500, currTx: 500,
            prevRx: 1000, prevTx: 2000);

        Assert.Equal(0UL, rx);
        Assert.Equal(0UL, tx);
    }

    [Fact(Timeout = 30000)]
    public void CalculateDelta_returns_zero_when_no_previous()
    {
        var (rx, tx) = NetworkCalculator.CalculateDelta(
            currRx: 1500, currTx: 2500,
            prevRx: null, prevTx: null);

        Assert.Equal(0UL, rx);
        Assert.Equal(0UL, tx);
    }

    [Fact(Timeout = 30000)]
    public void TruncateName_returns_short_name_unchanged()
    {
        Assert.Equal("eth0", NetworkCalculator.TruncateName("eth0", 20));
    }

    [Fact(Timeout = 30000)]
    public void TruncateName_truncates_long_name_with_ellipsis()
    {
        var longName = "Very Long Network Interface Name";
        var result = NetworkCalculator.TruncateName(longName, 20);

        Assert.Equal("Very Long Network In...", result);
        Assert.Equal(23, result.Length);
    }

    [Fact(Timeout = 30000)]
    public void ShouldInclude_returns_true_for_up_interface_with_speed()
    {
        Assert.True(NetworkCalculator.ShouldInclude(isUp: true, speed: 1_000_000));
    }

    [Fact(Timeout = 30000)]
    public void ShouldInclude_returns_false_for_down_interface()
    {
        Assert.False(NetworkCalculator.ShouldInclude(isUp: false, speed: 1_000_000));
    }

    [Fact(Timeout = 30000)]
    public void ShouldInclude_returns_false_for_zero_speed()
    {
        Assert.False(NetworkCalculator.ShouldInclude(isUp: true, speed: 0));
    }

    [Fact(Timeout = 30000)]
    public void BuildSnapshots_integrates_all_logic()
    {
        var interfaces = new[]
        {
            new NetworkCalculator.RawInterface("eth0", true, 1000, 100, 200),
            new NetworkCalculator.RawInterface("lo", false, 0, 50, 50),
            new NetworkCalculator.RawInterface("A Very Long Interface Name Here", true, 1000, 300, 400),
        };

        var prev = new Dictionary<string, (long Rx, long Tx)>
        {
            ["eth0"] = (50, 100),
        };

        var (snapshots, nextState) = NetworkCalculator.BuildSnapshots(interfaces, prev);

        Assert.Equal(2, snapshots.Count);
        Assert.Equal("eth0", snapshots[0].Name);
        Assert.Equal(50UL, snapshots[0].RxBytesPerSec);
        Assert.Equal(100UL, snapshots[0].TxBytesPerSec);
        Assert.Equal("A Very Long Interfac...", snapshots[1].Name);
        Assert.Equal(0UL, snapshots[1].RxBytesPerSec);
    }
}
