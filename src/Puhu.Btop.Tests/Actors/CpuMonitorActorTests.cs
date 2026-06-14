using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Tests.Actors;

public sealed class CpuMonitorActorTests : TestKit
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly ICpuMetrics _cpuMetrics = Substitute.For<ICpuMetrics>();
    private readonly ForwardingMetricSink _sink = new();

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton(_cpuMetrics);
        services.AddSingleton<IMetricSink>(_sink);
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry, resolver) =>
            registry.Register<CpuMonitorActor>(system.ActorOf(resolver.Props<CpuMonitorActor>(), "cpu-monitor")));

    [Fact(Timeout = 30000)]
    public async Task Tick_Samples_AndPublishesToSink()
    {
        _cpuMetrics.ProcessorName.Returns("Test CPU");
        _cpuMetrics.Measure().Returns(new CpuMeasurement(42.0, [10, 20, 30, 40]));
        var probe = CreateTestProbe();
        _sink.Target = probe;

        ActorRegistry.Get<CpuMonitorActor>().Tell(new Tick(0, TimeSpan.FromMilliseconds(500)));

        var snapshot = await probe.ExpectMsgAsync<CpuSnapshot>(cancellationToken: Ct);
        Assert.Equal("Test CPU", snapshot.Name);
        Assert.Equal(42.0, snapshot.TotalPercent);
        Assert.Equal(4, snapshot.CorePercents.Count);
    }

    [Fact(Timeout = 30000)]
    public async Task NoTick_NoPublish()
    {
        var probe = CreateTestProbe();
        _sink.Target = probe;

        _ = ActorRegistry.Get<CpuMonitorActor>();

        await probe.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), Ct);
    }
}
