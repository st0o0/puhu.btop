namespace Puhu.Btop.Core.Platform;

public static class CpuCalculator
{
    public readonly record struct State(
        long Idle, long Total, long[] CoreIdle, long[] CoreTotal)
    {
        public static State Initial(int coreCount) =>
            new(0, 0, new long[coreCount], new long[coreCount]);
    }

    public readonly record struct Result(CpuMeasurement Measurement, State NextState);

    public static double CalculatePercent(long prevIdle, long prevTotal, long currIdle, long currTotal)
    {
        var idleDelta = currIdle - prevIdle;
        var totalDelta = currTotal - prevTotal;
        var pct = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
        return Math.Clamp(pct, 0, 100);
    }

    public static Result Calculate(
        long currIdle, long currTotal,
        long[] currCoreIdle, long[] currCoreTotal,
        State prev)
    {
        var totalPercent = CalculatePercent(prev.Idle, prev.Total, currIdle, currTotal);

        var coreCount = currCoreIdle.Length;
        var cores = new List<double>(coreCount);
        for (var i = 0; i < coreCount; i++)
        {
            cores.Add(CalculatePercent(
                prev.CoreIdle[i], prev.CoreTotal[i],
                currCoreIdle[i], currCoreTotal[i]));
        }

        var nextState = new State(currIdle, currTotal, currCoreIdle, currCoreTotal);
        return new Result(new CpuMeasurement(totalPercent, cores), nextState);
    }
}
