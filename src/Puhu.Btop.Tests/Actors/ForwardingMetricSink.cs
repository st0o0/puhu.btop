using Akka.Actor;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Services;

namespace Puhu.Btop.Tests.Actors;

/// <summary>
/// Test double for <see cref="IMetricSink"/> that forwards every published snapshot to a
/// TestProbe. Lets actor tests assert publications with ExpectMsg/ExpectNoMsg instead of
/// polling a mock with a fixed delay. The <see cref="Target"/> is set in the test once the
/// probe exists; until then publishes are dropped.
/// </summary>
internal sealed class ForwardingMetricSink : IMetricSink
{
    public IActorRef? Target { get; set; }

    public void Publish(CpuSnapshot snapshot) => Target?.Tell(snapshot);
    public void Publish(MemorySnapshot snapshot) => Target?.Tell(snapshot);
    public void Publish(GpuSnapshot snapshot) => Target?.Tell(snapshot);
    public void Publish(IReadOnlyList<DiskSnapshot> snapshots) => Target?.Tell(snapshots);
    public void Publish(IReadOnlyList<NetworkSnapshot> snapshots) => Target?.Tell(snapshots);
    public void Publish(IReadOnlyList<ProcessSnapshot> snapshots) => Target?.Tell(snapshots);
    public void Publish(IReadOnlyList<ConnectionSnapshot> snapshots) => Target?.Tell(snapshots);
}
