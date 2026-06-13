using Puhu.Btop.Core.Models;

namespace Puhu.Btop.Services;

/// <summary>Actors publish snapshots here. Implemented by MetricStore.</summary>
public interface IMetricSink
{
    void Publish(CpuSnapshot snapshot);
    void Publish(MemorySnapshot snapshot);
    void Publish(GpuSnapshot snapshot);
    void Publish(IReadOnlyList<DiskSnapshot> snapshots);
    void Publish(IReadOnlyList<NetworkSnapshot> snapshots);
    void Publish(IReadOnlyList<ProcessSnapshot> snapshots);
    void Publish(IReadOnlyList<ConnectionSnapshot> snapshots);
}
