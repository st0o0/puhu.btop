using System.Collections.Concurrent;
using Akka.Actor;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Messages;
using Tick = Puhu.Plugin.Tick;
using RegisterMonitor = Puhu.Btop.Actors.RegisterMonitor;
using DemandChanged = Puhu.Btop.Actors.DemandChanged;

namespace Puhu.Btop.Tests.Actors;

/// <summary>Tests for TickRouter demand distribution logic (using a standalone TickRouter instance).</summary>
public class MonitoringSupervisorTickTests : IAsyncLifetime
{
    private ActorSystem _sys = null!;

    public ValueTask InitializeAsync()
    {
        _sys = ActorSystem.Create("tick-router-test");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _sys.Terminate();

    private IActorRef CreateRecorder(ConcurrentQueue<Tick> queue) =>
        _sys.ActorOf(Akka.Actor.Props.Create(() => new RecorderActor(queue)));

    [Fact]
    public async Task AlwaysOn_ReceivesEveryTick_WithoutDemand()
    {
        var received = new ConcurrentQueue<Tick>();
        var recorder = CreateRecorder(received);
        var router = _sys.ActorOf(Akka.Actor.Props.Create(() => new TickRouter()));

        router.Tell(new RegisterMonitor(MetricKind.Cpu, recorder, AlwaysOn: true, MinInterval: null));

        router.Tell(new Tick(0, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(1, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(2, TimeSpan.FromMilliseconds(1000)));

        await Task.Delay(300);

        Assert.Equal(3, received.Count);
    }

    [Fact]
    public async Task OnDemand_WithNoDemand_ReceivesNothing()
    {
        var received = new ConcurrentQueue<Tick>();
        var recorder = CreateRecorder(received);
        var router = _sys.ActorOf(Akka.Actor.Props.Create(() => new TickRouter()));

        router.Tell(new RegisterMonitor(MetricKind.Disk, recorder, AlwaysOn: false, MinInterval: null));

        router.Tell(new Tick(0, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(1, TimeSpan.FromMilliseconds(1000)));

        await Task.Delay(300);

        Assert.Equal(0, received.Count);
    }

    [Fact]
    public async Task DemandPlusOne_ReceivesTicks_ThenMinusOne_StopsTicks()
    {
        var received = new ConcurrentQueue<Tick>();
        var recorder = CreateRecorder(received);
        var router = _sys.ActorOf(Akka.Actor.Props.Create(() => new TickRouter()));

        router.Tell(new RegisterMonitor(MetricKind.Network, recorder, AlwaysOn: false, MinInterval: null));

        // Raise demand
        router.Tell(new DemandChanged(MetricKind.Network, +1));
        await Task.Delay(100); // allow demand message to be processed

        router.Tell(new Tick(0, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(1, TimeSpan.FromMilliseconds(1000)));
        await Task.Delay(200);

        Assert.Equal(2, received.Count);

        // Drop demand
        router.Tell(new DemandChanged(MetricKind.Network, -1));
        await Task.Delay(100); // allow demand message to be processed

        router.Tell(new Tick(2, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(3, TimeSpan.FromMilliseconds(1000)));
        await Task.Delay(200);

        Assert.Equal(2, received.Count); // no new ticks forwarded
    }

    [Fact]
    public async Task MinInterval_3s_At_1000msBase_OnlyForwardsSeq_0_3_6()
    {
        var received = new ConcurrentQueue<Tick>();
        var recorder = CreateRecorder(received);
        var router = _sys.ActorOf(Akka.Actor.Props.Create(() => new TickRouter()));

        // AlwaysOn, MinInterval = 3s → every = ceil(3000/1000) = 3, so seq % 3 == 0
        router.Tell(new RegisterMonitor(MetricKind.Gpu, recorder, AlwaysOn: true, MinInterval: TimeSpan.FromSeconds(3)));

        for (long seq = 0; seq <= 6; seq++)
        {
            router.Tell(new Tick(seq, TimeSpan.FromMilliseconds(1000)));
        }

        await Task.Delay(400);

        // Should receive seq 0, 3, 6 only (3 ticks)
        Assert.Equal(3, received.Count);
        var seqs = received.Select(t => t.Seq).OrderBy(s => s).ToList();
        Assert.Equal([0L, 3L, 6L], seqs);
    }

    private sealed class RecorderActor : ReceiveActor
    {
        public RecorderActor(ConcurrentQueue<Tick> queue)
        {
            Receive<Tick>(t => queue.Enqueue(t));
        }
    }
}
