using Akka.Actor;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;
using NSubstitute;

namespace Puhu.Btop.Tests.Actors;

public class CpuMonitorActorTests : IAsyncLifetime
{
    private readonly ICpuMetrics _cpuMetrics = Substitute.For<ICpuMetrics>();
    private readonly IMetricSink _sink = Substitute.For<IMetricSink>();
    private ActorSystem _sys = null!;

    public ValueTask InitializeAsync()
    {
        _sys = ActorSystem.Create("test");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _sys.Terminate();

    [Fact]
    public async Task Tick_Samples_AndPublishesToSink()
    {
        _cpuMetrics.ProcessorName.Returns("Test CPU");
        _cpuMetrics.Measure().Returns(new CpuMeasurement(42.0, [10, 20, 30, 40]));
        var actor = _sys.ActorOf(CpuMonitorActor.Props(_cpuMetrics, _sink));

        actor.Tell(new Tick(0, TimeSpan.FromMilliseconds(500)));
        await Task.Delay(200, TestContext.Current.CancellationToken);

        _sink.Received(1).Publish(Arg.Is<CpuSnapshot>(s =>
            s.Name == "Test CPU" && s.TotalPercent == 42.0 && s.CorePercents.Count == 4));
    }

    [Fact]
    public async Task NoTick_NoPublish()
    {
        var actor = _sys.ActorOf(CpuMonitorActor.Props(_cpuMetrics, _sink));

        await Task.Delay(200, TestContext.Current.CancellationToken);

        _sink.DidNotReceive().Publish(Arg.Any<CpuSnapshot>());
    }
}
