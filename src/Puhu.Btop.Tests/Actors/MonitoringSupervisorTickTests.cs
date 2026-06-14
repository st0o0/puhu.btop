using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Messages;
using Tick = Puhu.Plugin.Tick;
using RegisterMonitor = Puhu.Btop.Actors.RegisterMonitor;
using DemandChanged = Puhu.Btop.Actors.DemandChanged;

namespace Puhu.Btop.Tests.Actors;

/// <summary>Tests for TickRouter demand distribution logic (using a standalone TickRouter instance).</summary>
public sealed class MonitoringSupervisorTickTests : TestKit
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        // No actors at startup — each test spins up its own TickRouter.
    }

    private IActorRef CreateRouter() => Sys.ActorOf(Props.Create(() => new TickRouter()));

    [Fact(Timeout = 30000)]
    public async Task AlwaysOn_ReceivesEveryTick_WithoutDemand()
    {
        var probe = CreateTestProbe();
        var router = CreateRouter();
        router.Tell(new RegisterMonitor(MetricKind.Cpu, probe, AlwaysOn: true, MinInterval: null));

        router.Tell(new Tick(0, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(1, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(2, TimeSpan.FromMilliseconds(1000)));

        await probe.ExpectMsgAsync<Tick>(cancellationToken: Ct);
        await probe.ExpectMsgAsync<Tick>(cancellationToken: Ct);
        await probe.ExpectMsgAsync<Tick>(cancellationToken: Ct);
    }

    [Fact(Timeout = 30000)]
    public async Task OnDemand_WithNoDemand_ReceivesNothing()
    {
        var probe = CreateTestProbe();
        var router = CreateRouter();
        router.Tell(new RegisterMonitor(MetricKind.Disk, probe, AlwaysOn: false, MinInterval: null));

        router.Tell(new Tick(0, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(1, TimeSpan.FromMilliseconds(1000)));

        await probe.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), Ct);
    }

    [Fact(Timeout = 30000)]
    public async Task DemandPlusOne_ReceivesTicks_ThenMinusOne_StopsTicks()
    {
        var probe = CreateTestProbe();
        var router = CreateRouter();
        router.Tell(new RegisterMonitor(MetricKind.Network, probe, AlwaysOn: false, MinInterval: null));

        // Raise demand, then ticks flow. Messages to one actor are ordered, so the
        // demand is processed before the ticks — no sleep needed.
        router.Tell(new DemandChanged(MetricKind.Network, +1));
        router.Tell(new Tick(0, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(1, TimeSpan.FromMilliseconds(1000)));
        await probe.ExpectMsgAsync<Tick>(cancellationToken: Ct);
        await probe.ExpectMsgAsync<Tick>(cancellationToken: Ct);

        // Drop demand → no further ticks forwarded.
        router.Tell(new DemandChanged(MetricKind.Network, -1));
        router.Tell(new Tick(2, TimeSpan.FromMilliseconds(1000)));
        router.Tell(new Tick(3, TimeSpan.FromMilliseconds(1000)));
        await probe.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), Ct);
    }

    [Fact(Timeout = 30000)]
    public async Task MinInterval_3s_At_1000msBase_OnlyForwardsSeq_0_3_6()
    {
        var probe = CreateTestProbe();
        var router = CreateRouter();

        // AlwaysOn, MinInterval = 3s → every = ceil(3000/1000) = 3, so seq % 3 == 0.
        router.Tell(new RegisterMonitor(MetricKind.Gpu, probe, AlwaysOn: true, MinInterval: TimeSpan.FromSeconds(3)));

        for (long seq = 0; seq <= 6; seq++)
        {
            router.Tell(new Tick(seq, TimeSpan.FromMilliseconds(1000)));
        }

        var t0 = await probe.ExpectMsgAsync<Tick>(cancellationToken: Ct);
        var t3 = await probe.ExpectMsgAsync<Tick>(cancellationToken: Ct);
        var t6 = await probe.ExpectMsgAsync<Tick>(cancellationToken: Ct);
        Assert.Equal([0L, 3L, 6L], new[] { t0.Seq, t3.Seq, t6.Seq });
        await probe.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), Ct);
    }
}
