using Akka.Actor;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class NetworkMonitorActor : ReceiveActor
{
    public static Props Props(INetworkMetrics networkMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new NetworkMonitorActor(networkMetrics, sink));

    public NetworkMonitorActor(INetworkMetrics networkMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            var snapshots = networkMetrics.Measure().ToList();
            sink.Publish(snapshots);
        });
    }
}
