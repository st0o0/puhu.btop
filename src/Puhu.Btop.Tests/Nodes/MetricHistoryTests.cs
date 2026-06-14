using Puhu.Btop.Services;

namespace Puhu.Btop.Tests.Nodes;

public class MetricHistoryTests
{
    [Fact(Timeout = 30000)]
    public void Snapshot_returns_values_oldest_to_newest()
    {
        var history = new MetricHistory(capacity: 8);
        history.Push(10);
        history.Push(20);
        history.Push(30);

        Assert.Equal(new double[] { 10, 20, 30 }, history.Snapshot());
    }

    [Fact(Timeout = 30000)]
    public void Push_beyond_capacity_drops_oldest()
    {
        var history = new MetricHistory(capacity: 3);
        history.Push(1);
        history.Push(2);
        history.Push(3);
        history.Push(4);

        Assert.Equal(new double[] { 2, 3, 4 }, history.Snapshot());
        Assert.Equal(3, history.Count);
    }

    [Fact(Timeout = 30000)]
    public void Snapshot_with_maxPoints_returns_only_most_recent()
    {
        var history = new MetricHistory(capacity: 100);
        for (var i = 1; i <= 10; i++)
        {
            history.Push(i);
        }

        Assert.Equal(new double[] { 8, 9, 10 }, history.Snapshot(3));
    }

    [Fact(Timeout = 30000)]
    public void Snapshot_with_maxPoints_larger_than_count_returns_all()
    {
        var history = new MetricHistory(capacity: 100);
        history.Push(5);
        history.Push(6);

        Assert.Equal(new double[] { 5, 6 }, history.Snapshot(10));
    }

    [Fact(Timeout = 30000)]
    public void Empty_history_returns_empty_snapshot()
    {
        var history = new MetricHistory();
        Assert.Empty(history.Snapshot());
        Assert.Equal(0, history.Count);
    }

    [Fact(Timeout = 30000)]
    public async Task Concurrent_pushes_do_not_corrupt_buffer()
    {
        var history = new MetricHistory(capacity: 50);

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                history.Push(i);
            }
        }));

        await Task.WhenAll(tasks);

        // No exception + bounded size is the contract under concurrency.
        Assert.Equal(50, history.Count);
        Assert.Equal(50, history.Snapshot().Length);
    }
}
