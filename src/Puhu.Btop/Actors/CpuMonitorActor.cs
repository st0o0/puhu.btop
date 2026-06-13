using Akka.Actor;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class CpuMonitorActor : ReceiveActor
{
    public static Props Props(ICpuMetrics cpuMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new CpuMonitorActor(cpuMetrics, sink));

    public CpuMonitorActor(ICpuMetrics cpuMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            var measurement = cpuMetrics.Measure();
            sink.Publish(new CpuSnapshot(cpuMetrics.ProcessorName, measurement.TotalPercent, measurement.CorePercents));
        });
    }
}
