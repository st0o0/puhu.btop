namespace Puhu.Btop.Services;

public sealed class MetricHistory
{
    private readonly Queue<double> _data;
    private readonly int _capacity;
    private readonly Lock _gate = new();

    public MetricHistory(int capacity = 300)
    {
        _capacity = Math.Max(1, capacity);
        _data = new Queue<double>(_capacity);
    }

    /// <summary>Create a history pre-filled with existing samples (oldest → newest).</summary>
    public static MetricHistory From(IEnumerable<double> values, int capacity = 300)
    {
        var history = new MetricHistory(capacity);
        foreach (var value in values)
        {
            history.Push(value);
        }

        return history;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _data.Count;
            }
        }
    }

    public void Push(double value)
    {
        lock (_gate)
        {
            _data.Enqueue(value);
            while (_data.Count > _capacity)
            {
                _data.Dequeue();
            }
        }
    }

    /// <summary>All samples, oldest → newest.</summary>
    public double[] Snapshot()
    {
        lock (_gate)
        {
            return _data.ToArray();
        }
    }

    /// <summary>The most recent <paramref name="maxPoints"/> samples, oldest → newest.</summary>
    public double[] Snapshot(int maxPoints)
    {
        if (maxPoints <= 0)
        {
            return [];
        }

        lock (_gate)
        {
            var count = _data.Count;
            if (count <= maxPoints)
            {
                return _data.ToArray();
            }

            var result = new double[maxPoints];
            var skip = count - maxPoints;
            var i = 0;
            foreach (var value in _data)
            {
                if (i >= skip)
                {
                    result[i - skip] = value;
                }

                i++;
            }

            return result;
        }
    }
}
