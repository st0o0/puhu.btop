namespace Puhu.Btop.Platform.Linux;

using Puhu.Btop.Core.Platform;

public sealed class LinuxMemoryMetrics : IMemoryMetrics
{
    public (ulong TotalBytes, ulong UsedBytes) Measure()
    {
        try
        {
            ulong total = 0, available = 0;
            foreach (var line in File.ReadAllLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:"))
                {
                    total = ParseKb(line) * 1024;
                }
                else if (line.StartsWith("MemAvailable:"))
                {
                    available = ParseKb(line) * 1024;
                }
            }

            if (total > 0)
            {
                return (total, total - available);
            }
        }
        catch
        {
        }

        return (0, 0);
    }

    private static ulong ParseKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && ulong.TryParse(parts[1], out var val) ? val : 0;
    }
}
