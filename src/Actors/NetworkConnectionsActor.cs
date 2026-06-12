using Akka.Actor;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

public sealed class NetworkConnectionsActor : ReceiveActor
{
    public static Props Props(IConnectionProvider provider, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new NetworkConnectionsActor(provider, sink));

    public NetworkConnectionsActor(IConnectionProvider provider, IMetricSink sink)
    {
        Receive<Tick>(_ => sink.Publish(provider.GetConnections()));
    }
}
