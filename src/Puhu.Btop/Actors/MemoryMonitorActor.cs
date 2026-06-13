using Akka.Actor;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class MemoryMonitorActor : ReceiveActor
{
    public static Props Props(IMemoryMetrics memoryMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new MemoryMonitorActor(memoryMetrics, sink));

    public MemoryMonitorActor(IMemoryMetrics memoryMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            var (total, used) = memoryMetrics.Measure();
            sink.Publish(new MemorySnapshot(total, used));
        });
    }
}
