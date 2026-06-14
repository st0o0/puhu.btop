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

public sealed class ProcessMonitorActorTests : TestKit
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IProcessClassifier _classifier = Substitute.For<IProcessClassifier>();
    private readonly IProcessTreeProvider _treeProvider = Substitute.For<IProcessTreeProvider>();
    private readonly ForwardingMetricSink _sink = new();

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton(_classifier);
        services.AddSingleton(_treeProvider);
        services.AddSingleton<IMetricSink>(_sink);
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry, resolver) =>
            registry.Register<ProcessMonitorActor>(system.ActorOf(resolver.Props<ProcessMonitorActor>(), "process-monitor")));

    [Fact(Timeout = 30000)]
    public async Task Tick_PopulatesParentPidFromTreeProvider()
    {
        // The tree provider knows this (real, running) test process's parent.
        const int sentinelParent = 424242;
        var selfPid = Environment.ProcessId;
        _classifier.Classify(Arg.Any<System.Diagnostics.Process>()).Returns(ProcessGroup.Apps);
        _treeProvider.ReadParentMap().Returns(new Dictionary<int, int> { [selfPid] = sentinelParent });

        var probe = CreateTestProbe();
        _sink.Target = probe;

        ActorRegistry.Get<ProcessMonitorActor>().Tell(new Tick(0, TimeSpan.FromMilliseconds(500)));

        var published = await probe.ExpectMsgAsync<IReadOnlyList<ProcessSnapshot>>(cancellationToken: Ct);
        var self = published.FirstOrDefault(p => p.Pid == selfPid);
        Assert.NotNull(self);
        Assert.Equal(sentinelParent, self!.ParentPid);
    }
}
