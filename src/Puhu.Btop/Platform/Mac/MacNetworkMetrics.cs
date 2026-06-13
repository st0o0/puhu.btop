using System.Net.NetworkInformation;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Mac;

public sealed class MacNetworkMetrics : INetworkMetrics
{
    private Dictionary<string, (long Rx, long Tx)>? _prevBytes;

    public IReadOnlyList<NetworkSnapshot> Measure()
    {
        try
        {
            var raw = new List<NetworkCalculator.RawInterface>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                var stats = ni.GetIPv4Statistics();
                raw.Add(new NetworkCalculator.RawInterface(
                    ni.Name,
                    ni.OperationalStatus == OperationalStatus.Up,
                    ni.Speed,
                    stats.BytesReceived,
                    stats.BytesSent));
            }

            var (snapshots, nextState) = NetworkCalculator.BuildSnapshots(raw, _prevBytes);
            _prevBytes = nextState;
            return snapshots;
        }
        catch
        {
            return [];
        }
    }
}
