using Akka.Actor;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;
using NSubstitute;

namespace Puhu.Btop.Tests.Actors;

public class ProcessMonitorActorTests : IAsyncLifetime
{
    private readonly IProcessClassifier _classifier = Substitute.For<IProcessClassifier>();
    private readonly IProcessTreeProvider _treeProvider = Substitute.For<IProcessTreeProvider>();
    private readonly IMetricSink _sink = Substitute.For<IMetricSink>();
    private ActorSystem _sys = null!;

    public ValueTask InitializeAsync()
    {
        _sys = ActorSystem.Create("test");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _sys.Terminate();

    [Fact]
    public async Task Tick_PopulatesParentPidFromTreeProvider()
    {
        // The tree provider knows this (real, running) test process's parent.
        const int sentinelParent = 424242;
        var selfPid = Environment.ProcessId;
        _classifier.Classify(Arg.Any<System.Diagnostics.Process>()).Returns(ProcessGroup.Apps);
        _treeProvider.ReadParentMap().Returns(new Dictionary<int, int> { [selfPid] = sentinelParent });

        IReadOnlyList<ProcessSnapshot>? published = null;
        _sink.Publish(Arg.Do<IReadOnlyList<ProcessSnapshot>>(list => published = list));

        var actor = _sys.ActorOf(ProcessMonitorActor.Props(_classifier, _treeProvider, _sink));
        actor.Tell(new Tick(0, TimeSpan.FromMilliseconds(500)));
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.NotNull(published);
        var self = published!.FirstOrDefault(p => p.Pid == selfPid);
        Assert.NotNull(self);
        Assert.Equal(sentinelParent, self!.ParentPid);
    }
}
