using Akka.Actor;
using Puhu.Btop.Core.Messages;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

internal sealed record DemandChanged(MetricKind Kind, int Delta);
internal sealed record RegisterMonitor(MetricKind Kind, IActorRef Actor, bool AlwaysOn, TimeSpan? MinInterval);

/// <summary>Distributes refresh ticks to registered monitors based on demand and per-monitor minimum intervals.</summary>
public class TickRouter : ReceiveActor
{
    private readonly Dictionary<MetricKind, (IActorRef Actor, bool AlwaysOn, TimeSpan? MinInterval)> _monitors = new();
    private readonly Dictionary<MetricKind, int> _demand = new();

    public TickRouter()
    {
        Receive<RegisterMonitor>(m => _monitors[m.Kind] = (m.Actor, m.AlwaysOn, m.MinInterval));
        Receive<DemandChanged>(m =>
            _demand[m.Kind] = Math.Max(0, _demand.GetValueOrDefault(m.Kind) + m.Delta));
        Receive<Tick>(tick =>
        {
            foreach (var (kind, reg) in _monitors)
            {
                if (!reg.AlwaysOn && _demand.GetValueOrDefault(kind) == 0)
                {
                    continue;
                }

                if (reg.MinInterval is { } min)
                {
                    var every = Math.Max(1, (int)Math.Ceiling(min.TotalMilliseconds / tick.BaseInterval.TotalMilliseconds));
                    if (tick.Seq % every != 0)
                    {
                        continue;
                    }
                }

                reg.Actor.Tell(tick);
            }
        });
    }
}
