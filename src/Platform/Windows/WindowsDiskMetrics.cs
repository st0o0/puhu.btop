using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsDiskMetrics : IDiskMetrics
{
    private IntPtr _query;
    private readonly Dictionary<string, (IntPtr Read, IntPtr Write, IntPtr Active)> _counters = new();
    private volatile bool _ready;

    public void Initialize()
    {
        if (_ready)
        {
            return;
        }

        try
        {
            if (PdhOpenQueryW(null, IntPtr.Zero, out _query) != 0)
            {
                return;
            }

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || drive.Name.Length < 2)
                {
                    continue;
                }

                var instance = drive.Name[..2]; // "C:"
                var read = AddCounter($@"\LogicalDisk({instance})\Disk Read Bytes/sec");
                var write = AddCounter($@"\LogicalDisk({instance})\Disk Write Bytes/sec");
                var active = AddCounter($@"\LogicalDisk({instance})\% Disk Time");
                if (read != IntPtr.Zero && write != IntPtr.Zero && active != IntPtr.Zero)
                {
                    _counters[instance] = (read, write, active);
                }
            }

            PdhCollectQueryData(_query);
            _ready = _counters.Count > 0;
        }
        catch
        {
            _ready = false;
        }
    }

    public (ulong ReadBytesPerSec, ulong WriteBytesPerSec, double ActivePercent) GetMetrics(string diskName)
    {
        if (!_ready || !_counters.TryGetValue(diskName, out var c))
        {
            return (0, 0, 0);
        }

        try
        {
            PdhCollectQueryData(_query);
            var read = (ulong)Math.Max(0, GetValue(c.Read));
            var write = (ulong)Math.Max(0, GetValue(c.Write));
            var active = Math.Clamp(GetValue(c.Active), 0, 100);
            return (read, write, active);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    private IntPtr AddCounter(string path) =>
        PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out var counter) == 0 ? counter : IntPtr.Zero;

    private static double GetValue(IntPtr counter)
    {
        if (PdhGetFormattedCounterValue(counter, PdhFmtDouble, IntPtr.Zero, out var value) != 0)
        {
            return 0;
        }
        return value.CStatus == 0 ? value.DoubleValue : 0;
    }

    private const uint PdhFmtDouble = 0x00000200;

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValue
    {
        public uint CStatus;
        public double DoubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr hQuery);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, IntPtr lpdwType, out PdhFmtCounterValue pValue);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr hQuery);
}
