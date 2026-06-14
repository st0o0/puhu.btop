using Puhu.Btop.Core.Models;
using Puhu.Btop.Services;

namespace Puhu.Btop.Tests.Services;

public class MetricStoreTests
{
    [Fact(Timeout = 30000)]
    public void PublishCpu_SetsLatest_AndPushesHistory()
    {
        var store = new MetricStore();

        store.Publish(new CpuSnapshot("cpu", 42.0, [40, 44]));

        Assert.Equal(42.0, store.Cpu.Value!.TotalPercent);
        Assert.Equal([42.0], store.CpuHistory.Snapshot());
    }

    [Fact(Timeout = 30000)]
    public void PublishMemory_PushesUsedPercentHistory()
    {
        var store = new MetricStore();

        store.Publish(new MemorySnapshot(100, 25));

        Assert.Equal([25.0], store.MemHistory.Snapshot());
    }

    [Fact(Timeout = 30000)]
    public void KeyedHistory_SameKey_SameInstance()
    {
        var store = new MetricStore();

        var a = store.History("disk:C:active");
        var b = store.History("disk:C:active");

        Assert.Same(a, b);
    }

    [Fact(Timeout = 30000)]
    public void KeyedHistory_EvictsLeastRecentlyUsed_BeyondLimit()
    {
        var store = new MetricStore(keyedHistoryLimit: 2);

        var first = store.History("a");
        store.History("b");
        store.History("c");           // evicts "a"
        var again = store.History("a");

        Assert.NotSame(first, again);
    }

    [Fact(Timeout = 30000)]
    public void ConcurrentPublish_DoesNotThrow_AndKeepsHistoryConsistent()
    {
        var store = new MetricStore();

        Parallel.For(0, 1000, i =>
        {
            store.Publish(new CpuSnapshot("cpu", i % 100, [1.0]));
            store.History($"pid:{i % 10}").Push(i);
        });

        // MetricHistory capacity is 300; after 1000 pushes the ring buffer holds 300 entries.
        Assert.Equal(300, store.CpuHistory.Snapshot().Length);
        Assert.NotNull(store.Cpu.Value);
    }
}
