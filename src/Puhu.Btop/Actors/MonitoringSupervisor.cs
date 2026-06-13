using Akka.Actor;
using Puhu.Btop.Core.Messages;
using Servus.Akka;

namespace Puhu.Btop.Actors;

public sealed class MonitoringSupervisor : TickRouter
{
    public MonitoringSupervisor()
    {
        // Children are resolved through the DI DependencyResolver: each actor's
        // constructor gets its platform services injected directly, so this
        // supervisor no longer has to thread services through Props factories.
        var cpu = Context.ResolveChildActor<CpuMonitorActor>("cpu-monitor");
        var memory = Context.ResolveChildActor<MemoryMonitorActor>("memory-monitor");
        var disk = Context.ResolveChildActor<DiskMonitorActor>("disk-monitor");
        var network = Context.ResolveChildActor<NetworkMonitorActor>("network-monitor");
        var gpu = Context.ResolveChildActor<GpuMonitorActor>("gpu-monitor");
        var processSupervisor = Context.ResolveChildActor<ProcessSupervisor>("process-supervisor");
        var connections = Context.ResolveChildActor<NetworkConnectionsActor>("network-connections");

        // Self-register monitors with the TickRouter
        Self.Tell(new RegisterMonitor(MetricKind.Cpu, cpu, true, null));
        Self.Tell(new RegisterMonitor(MetricKind.Memory, memory, true, null));
        Self.Tell(new RegisterMonitor(MetricKind.Disk, disk, false, null));
        Self.Tell(new RegisterMonitor(MetricKind.Network, network, false, null));
        Self.Tell(new RegisterMonitor(MetricKind.Gpu, gpu, false, null));
        Self.Tell(new RegisterMonitor(MetricKind.Process, processSupervisor, false, null));
        Self.Tell(new RegisterMonitor(MetricKind.NetworkConnections, connections, false, null));

        // Process action commands
        Receive<KillProcess>(msg => processSupervisor.Forward(msg));
        Receive<SetProcessPriority>(msg => processSupervisor.Forward(msg));
        Receive<SetProcessAffinity>(msg => processSupervisor.Forward(msg));
        Receive<GetProcessTree>(msg => processSupervisor.Forward(msg));
        Receive<GetProcessEnvironment>(msg => processSupervisor.Forward(msg));
        Receive<GetProcessHandles>(msg => processSupervisor.Forward(msg));
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 10,
            withinTimeRange: TimeSpan.FromSeconds(30),
            localOnlyDecider: ex =>
            {
                var directive = ex switch
                {
                    IOException => Directive.Resume,
                    UnauthorizedAccessException => Directive.Resume,
                    _ => Directive.Restart
                };
                return directive;
            });
}
