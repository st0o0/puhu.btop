using Akka.Actor;
using Puhu.Btop.Core.Messages;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

public sealed class ProcessSupervisor : ReceiveActor
{
    public ProcessSupervisor()
    {
        // Children resolved via the DI DependencyResolver — services are injected
        // straight into each child's constructor.
        var processMonitor = Context.ResolveChildActor<ProcessMonitorActor>("process-monitor");
        var processAction = Context.ResolveChildActor<ProcessActionActor>("process-action");

        // Forward ticks to the process monitor
        Receive<Tick>(msg => processMonitor.Forward(msg));

        // Process actions
        Receive<KillProcess>(msg => processAction.Forward(msg));
        Receive<SetProcessPriority>(msg => processAction.Forward(msg));
        Receive<SetProcessAffinity>(msg => processAction.Forward(msg));
        Receive<GetProcessTree>(msg => processAction.Forward(msg));
        Receive<GetProcessEnvironment>(msg => processAction.Forward(msg));
        Receive<GetProcessHandles>(msg => processAction.Forward(msg));
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 5,
            withinTimeRange: TimeSpan.FromSeconds(10),
            localOnlyDecider: ex =>
            {
                var directive = ex switch
                {
                    InvalidOperationException => Directive.Resume,
                    System.ComponentModel.Win32Exception => Directive.Resume,
                    _ => Directive.Restart
                };
                return directive;
            });
}
