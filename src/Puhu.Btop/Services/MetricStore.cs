using Puhu.Btop.Core.Models;
using R3;

namespace Puhu.Btop.Services;

public sealed class MetricStore : IMetricSink
{
    private readonly int _keyedHistoryLimit;
    private readonly Dictionary<string, MetricHistory> _keyed = new();
    private readonly LinkedList<string> _lru = new();
    private readonly Lock _gate = new();

    public ReactiveProperty<CpuSnapshot?> Cpu { get; } = new(null);
    public ReactiveProperty<MemorySnapshot?> Memory { get; } = new(null);
    public ReactiveProperty<GpuSnapshot?> Gpu { get; } = new(null);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<ProcessSnapshot>> Processes { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<ConnectionSnapshot>> Connections { get; } = new([]);

    public MetricHistory CpuHistory { get; } = new();
    public MetricHistory MemHistory { get; } = new();
    public MetricHistory GpuHistory { get; } = new();

    public MetricStore(int keyedHistoryLimit = 256)
    {
        _keyedHistoryLimit = keyedHistoryLimit;
    }

    public void Publish(CpuSnapshot snapshot)
    {
        lock (_gate)
        {
            CpuHistory.Push(snapshot.TotalPercent);
            Cpu.Value = snapshot;
        }
    }

    public void Publish(MemorySnapshot snapshot)
    {
        lock (_gate)
        {
            MemHistory.Push(snapshot.TotalBytes > 0 ? (double)snapshot.UsedBytes / snapshot.TotalBytes * 100 : 0);
            Memory.Value = snapshot;
        }
    }

    public void Publish(GpuSnapshot snapshot)
    {
        lock (_gate)
        {
            GpuHistory.Push(snapshot.UsagePercent);
            Gpu.Value = snapshot;
        }
    }

    public void Publish(IReadOnlyList<DiskSnapshot> snapshots)
    {
        lock (_gate)
        {
            foreach (var d in snapshots)
            {
                HistoryCore($"disk:{d.Name}:active").Push(d.ActiveTimePercent);
                HistoryCore($"disk:{d.Name}:transfer").Push(d.TransferBytesPerSec);
            }

            Disks.Value = snapshots;
        }
    }

    public void Publish(IReadOnlyList<NetworkSnapshot> snapshots)
    {
        lock (_gate)
        {
            Networks.Value = snapshots;
        }
    }

    public void Publish(IReadOnlyList<ProcessSnapshot> snapshots)
    {
        lock (_gate)
        {
            foreach (var p in snapshots)
            {
                HistoryCore($"pid:{p.Pid}").Push(p.CpuPercent);
            }

            Processes.Value = snapshots;
        }
    }

    public void Publish(IReadOnlyList<ConnectionSnapshot> snapshots)
    {
        lock (_gate)
        {
            Connections.Value = snapshots;
        }
    }

    /// <summary>
    /// Returns the <see cref="MetricHistory"/> for the given key; creates one if it does not yet exist.
    /// This is the deliberate read API for keyed histories (per-disk / per-PID / per-container).
    /// Thread-safe. Entries are LRU-evicted once the store exceeds the limit supplied at construction.
    /// </summary>
    public MetricHistory History(string key)
    {
        lock (_gate)
        {
            return HistoryCore(key);
        }
    }

    // Must be called under _gate.
    private MetricHistory HistoryCore(string key)
    {
        if (_keyed.TryGetValue(key, out var existing))
        {
            _lru.Remove(key);
            _lru.AddLast(key);
            return existing;
        }

        if (_keyed.Count >= _keyedHistoryLimit && _lru.First is { } oldest)
        {
            _keyed.Remove(oldest.Value);
            _lru.RemoveFirst();
        }

        var history = new MetricHistory();
        _keyed[key] = history;
        _lru.AddLast(key);
        return history;
    }
}
