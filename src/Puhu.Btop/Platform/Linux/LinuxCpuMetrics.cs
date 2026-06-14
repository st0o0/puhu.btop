namespace Puhu.Btop.Platform.Linux;

using Puhu.Btop.Core.Platform;

public sealed class LinuxCpuMetrics : ICpuMetrics
{
    private CpuCalculator.State _state;
    private string? _cpuName;

    public string ProcessorName => _cpuName ??= ReadCpuName();
    public int CoreCount => Environment.ProcessorCount;

    public CpuMeasurement Measure()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/stat");
            var (idle, total) = ParseRawCounters(lines[0]);

            var coreCount = Environment.ProcessorCount;
            var coreIdle = new long[coreCount];
            var coreTotal = new long[coreCount];
            for (var i = 0; i < coreCount; i++)
            {
                var lineIdx = i + 1;
                if (lineIdx < lines.Length && lines[lineIdx].StartsWith("cpu"))
                {
                    (coreIdle[i], coreTotal[i]) = ParseRawCounters(lines[lineIdx]);
                }
            }

            if (_state.CoreIdle is null)
            {
                _state = CpuCalculator.State.Initial(coreCount);
            }

            var result = CpuCalculator.Calculate(idle, total, coreIdle, coreTotal, _state);
            _state = result.NextState;
            return result.Measurement;
        }
        catch
        {
            return new CpuMeasurement(0, []);
        }
    }

    internal static (long Idle, long Total) ParseRawCounters(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            return (0, 0);
        }

        long user = long.Parse(parts[1]), nice = long.Parse(parts[2]);
        long system = long.Parse(parts[3]), idle = long.Parse(parts[4]);
        var total = user + nice + system + idle;
        for (var j = 5; j < Math.Min(parts.Length, 8); j++)
            if (long.TryParse(parts[j], out var v))
            {
                total += v;
            }

        return (idle, total);
    }

    private static string ReadCpuName()
    {
        try
        {
            var cpuinfo = File.ReadAllLines("/proc/cpuinfo");
            var nameLine = cpuinfo.FirstOrDefault(l => l.StartsWith("model name"));
            return nameLine?.Split(':').LastOrDefault()?.Trim() ?? "CPU";
        }
        catch { return "CPU"; }
    }
}
