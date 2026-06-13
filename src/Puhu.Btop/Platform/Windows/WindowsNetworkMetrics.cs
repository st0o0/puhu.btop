using System.Net.NetworkInformation;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Windows;

public sealed class WindowsNetworkMetrics : INetworkMetrics
{
    private Dictionary<string, (long Rx, long Tx)>? _prevBytes;

    public IReadOnlyList<NetworkSnapshot> Measure()
    {
        try
        {
            var raw = ReadRawInterfaces();
            var (snapshots, nextState) = NetworkCalculator.BuildSnapshots(raw, _prevBytes);
            _prevBytes = nextState;
            return snapshots;
        }
        catch
        {
            return [];
        }
    }

    private static List<NetworkCalculator.RawInterface> ReadRawInterfaces()
    {
        var result = new List<NetworkCalculator.RawInterface>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            var stats = ni.GetIPv4Statistics();
            result.Add(new NetworkCalculator.RawInterface(
                ni.Name,
                ni.OperationalStatus == OperationalStatus.Up,
                ni.Speed,
                stats.BytesReceived,
                stats.BytesSent));
        }
        return result;
    }
}
