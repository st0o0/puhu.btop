using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Tests.Core;

public class CpuCalculatorTests
{
    [Fact]
    public void CalculatePercent_returns_50_when_half_idle()
    {
        var result = CpuCalculator.CalculatePercent(
            prevIdle: 100, prevTotal: 200,
            currIdle: 150, currTotal: 300);

        Assert.Equal(50.0, result);
    }

    [Fact]
    public void CalculatePercent_returns_0_when_fully_idle()
    {
        var result = CpuCalculator.CalculatePercent(
            prevIdle: 100, prevTotal: 200,
            currIdle: 200, currTotal: 300);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CalculatePercent_returns_100_when_zero_idle()
    {
        var result = CpuCalculator.CalculatePercent(
            prevIdle: 100, prevTotal: 200,
            currIdle: 100, currTotal: 300);

        Assert.Equal(100.0, result);
    }

    [Fact]
    public void CalculatePercent_clamps_negative_to_0()
    {
        var result = CpuCalculator.CalculatePercent(
            prevIdle: 0, prevTotal: 100,
            currIdle: 200, currTotal: 200);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CalculatePercent_returns_0_when_totalDelta_is_zero()
    {
        var result = CpuCalculator.CalculatePercent(
            prevIdle: 100, prevTotal: 200,
            currIdle: 100, currTotal: 200);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Calculate_returns_total_and_per_core()
    {
        var prev = new CpuCalculator.State(
            Idle: 100, Total: 200,
            CoreIdle: [50, 60],
            CoreTotal: [100, 120]);

        var result = CpuCalculator.Calculate(
            currIdle: 150, currTotal: 300,
            currCoreIdle: [75, 90],
            currCoreTotal: [150, 180],
            prev);

        Assert.Equal(50.0, result.Measurement.TotalPercent);
        Assert.Equal(2, result.Measurement.CorePercents.Count);
        Assert.Equal(50.0, result.Measurement.CorePercents[0]);
        Assert.Equal(50.0, result.Measurement.CorePercents[1]);
    }

    [Fact]
    public void Calculate_returns_updated_state()
    {
        var prev = CpuCalculator.State.Initial(2);

        var result = CpuCalculator.Calculate(
            currIdle: 100, currTotal: 200,
            currCoreIdle: [50, 60],
            currCoreTotal: [100, 120],
            prev);

        Assert.Equal(100, result.NextState.Idle);
        Assert.Equal(200, result.NextState.Total);
        Assert.Equal([50, 60], result.NextState.CoreIdle);
        Assert.Equal([100, 120], result.NextState.CoreTotal);
    }

    [Fact]
    public void Calculate_with_initial_state_returns_100_percent()
    {
        var prev = CpuCalculator.State.Initial(2);

        var result = CpuCalculator.Calculate(
            currIdle: 50, currTotal: 200,
            currCoreIdle: [25, 30],
            currCoreTotal: [100, 120],
            prev);

        Assert.Equal(75.0, result.Measurement.TotalPercent);
    }
}
