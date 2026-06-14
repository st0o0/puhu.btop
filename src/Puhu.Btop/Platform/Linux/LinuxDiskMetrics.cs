using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Linux;

public sealed class LinuxDiskMetrics : IDiskMetrics
{
    private Dictionary<string, (ulong ReadSectors, ulong WriteSectors, ulong IoTicks, DateTime Timestamp)>? _previous;

    private const int SectorSize = 512;

    public void Initialize()
    {
        // Prime the initial reading so deltas can be computed on the next call
        _previous = ReadDiskStats();
    }

    public (ulong ReadBytesPerSec, ulong WriteBytesPerSec, double ActivePercent) GetMetrics(string diskName)
    {
        var current = ReadDiskStats();

        if (_previous is null || !_previous.TryGetValue(diskName, out var prev) ||
            !current.TryGetValue(diskName, out var cur))
        {
            _previous = current;
            return (0, 0, 0);
        }

        var elapsed = (cur.Timestamp - prev.Timestamp).TotalSeconds;
        if (elapsed <= 0)
        {
            _previous = current;
            return (0, 0, 0);
        }

        var readBytes = (cur.ReadSectors - prev.ReadSectors) * SectorSize;
        var writeBytes = (cur.WriteSectors - prev.WriteSectors) * SectorSize;
        var ioTicksDelta = cur.IoTicks - prev.IoTicks;
        var activePercent = Math.Clamp(ioTicksDelta / (elapsed * 1000) * 100, 0, 100);

        _previous = current;

        return ((ulong)(readBytes / elapsed), (ulong)(writeBytes / elapsed), activePercent);
    }

    public void Dispose()
    {
        _previous = null;
    }

    private static Dictionary<string, (ulong ReadSectors, ulong WriteSectors, ulong IoTicks, DateTime Timestamp)> ReadDiskStats()
    {
        var result = new Dictionary<string, (ulong, ulong, ulong, DateTime)>();
        var now = DateTime.UtcNow;

        try
        {
            foreach (var line in File.ReadAllLines("/proc/diskstats"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 14)
                {
                    continue;
                }

                var name = parts[2];
                // Skip partition entries (e.g. sda1), keep whole disks (sda, nvme0n1)
                if (name.Length > 2 && char.IsDigit(name[^1]) && !name.Contains("nvme"))
                {
                    continue;
                }

                if (ulong.TryParse(parts[5], out var readSectors) &&
                    ulong.TryParse(parts[9], out var writeSectors) &&
                    ulong.TryParse(parts[12], out var ioTicks))
                {
                    result[name] = (readSectors, writeSectors, ioTicks, now);
                }
            }
        }
        catch
        {
        }

        return result;
    }
}
