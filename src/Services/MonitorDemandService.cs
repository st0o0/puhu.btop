using Akka.Actor;
using Akka.Hosting;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Messages;

namespace Puhu.Btop.Services;

public interface IMonitorDemand
{
    IDisposable Acquire(MetricKind kind);
}

public sealed class MonitorDemandService(IRequiredActor<MonitoringSupervisor> supervisor) : IMonitorDemand
{
    public IDisposable Acquire(MetricKind kind)
    {
        Send(kind, +1);
        return new Releaser(() => Send(kind, -1));
    }

    private void Send(MetricKind kind, int delta) =>
        _ = supervisor.GetAsync(CancellationToken.None).ContinueWith(
            (Task<IActorRef> t) => t.Result.Tell(new DemandChanged(kind, delta), ActorRefs.NoSender),
            TaskContinuationOptions.OnlyOnRanToCompletion);

    private sealed class Releaser(Action release) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                release();
            }
        }
    }
}
