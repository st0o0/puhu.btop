using Akka.Actor;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

public sealed class ProcessSupervisor : ReceiveActor
{
    private readonly IActorRef _processMonitor;
    private readonly IActorRef _processAction;

    public static Props Props(
        IProcessClassifier classifier,
        IProcessTreeProvider treeProvider,
        IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new ProcessSupervisor(
            classifier, treeProvider, sink));

    public ProcessSupervisor(
        IProcessClassifier classifier,
        IProcessTreeProvider treeProvider,
        IMetricSink sink)
    {
        _processMonitor = Context.ActorOf(ProcessMonitorActor.Props(classifier, treeProvider, sink), "process-monitor");
        _processAction = Context.ActorOf(ProcessActionActor.Props(treeProvider), "process-action");

        // Forward ticks to the process monitor
        Receive<Tick>(msg => _processMonitor.Forward(msg));

        // Process actions
        Receive<KillProcess>(msg => _processAction.Forward(msg));
        Receive<SetProcessPriority>(msg => _processAction.Forward(msg));
        Receive<SetProcessAffinity>(msg => _processAction.Forward(msg));
        Receive<GetProcessTree>(msg => _processAction.Forward(msg));
        Receive<GetProcessEnvironment>(msg => _processAction.Forward(msg));
        Receive<GetProcessHandles>(msg => _processAction.Forward(msg));
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
