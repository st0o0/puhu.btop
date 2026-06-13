using Akka.Actor;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class GpuMonitorActor : ReceiveActor
{
    public static Props Props(IGpuMetrics gpuMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new GpuMonitorActor(gpuMetrics, sink));

    public GpuMonitorActor(IGpuMetrics gpuMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            if (!gpuMetrics.IsAvailable)
            {
                return;
            }

            var snapshot = gpuMetrics.GetSnapshot();
            sink.Publish(snapshot);
        });
    }
}
