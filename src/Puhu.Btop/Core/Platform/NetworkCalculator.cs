using Puhu.Btop.Core.Models;

namespace Puhu.Btop.Core.Platform;

public static class NetworkCalculator
{
    public readonly record struct RawInterface(string Name, bool IsUp, long Speed, long RxBytes, long TxBytes);

    public static (ulong RxPerSec, ulong TxPerSec) CalculateDelta(
        long currRx, long currTx, long? prevRx, long? prevTx)
    {
        if (prevRx is null || prevTx is null)
            return (0, 0);

        var rx = (ulong)Math.Max(0, currRx - prevRx.Value);
        var tx = (ulong)Math.Max(0, currTx - prevTx.Value);
        return (rx, tx);
    }

    public static string TruncateName(string name, int maxLength) =>
        name.Length > maxLength ? name[..maxLength] + "..." : name;

    public static bool ShouldInclude(bool isUp, long speed) =>
        isUp && speed > 0;

    public static (IReadOnlyList<NetworkSnapshot> Snapshots, Dictionary<string, (long Rx, long Tx)> NextState)
        BuildSnapshots(IReadOnlyList<RawInterface> interfaces, Dictionary<string, (long Rx, long Tx)>? prev)
    {
        var nextState = new Dictionary<string, (long Rx, long Tx)>();
        var snapshots = new List<NetworkSnapshot>();

        foreach (var ni in interfaces)
        {
            if (!ShouldInclude(ni.IsUp, ni.Speed))
                continue;

            var name = TruncateName(ni.Name, 20);
            nextState[name] = (ni.RxBytes, ni.TxBytes);

            long? prevRx = null, prevTx = null;
            if (prev is not null && prev.TryGetValue(name, out var p))
            {
                prevRx = p.Rx;
                prevTx = p.Tx;
            }

            var (rx, tx) = CalculateDelta(ni.RxBytes, ni.TxBytes, prevRx, prevTx);
            snapshots.Add(new NetworkSnapshot(name, rx, tx));
        }

        return (snapshots, nextState);
    }
}
