using Akka.Actor;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Platform;
using NSubstitute;

namespace Puhu.Btop.Tests.Actors;

public class ProcessActionActorTests : IAsyncLifetime
{
    private readonly IProcessTreeProvider _treeProvider = Substitute.For<IProcessTreeProvider>();
    private ActorSystem _sys = null!;

    public ValueTask InitializeAsync()
    {
        _sys = ActorSystem.Create("test");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _sys.Terminate();
    }

    [Fact]
    public async Task GetProcessTree_ReturnsTreeResult()
    {
        var expected = new ProcessTreeResult(1234, "test.exe", []);
        _treeProvider.BuildTree(1234).Returns(expected);

        var actor = _sys.ActorOf(ProcessActionActor.Props(_treeProvider));
        var result = await actor.Ask<ProcessTreeResult>(
            new GetProcessTree(1234), TimeSpan.FromSeconds(3));

        Assert.Equal(1234, result.Pid);
        Assert.Equal("test.exe", result.Name);
    }

    [Fact]
    public async Task KillProcess_WithInvalidPid_ReturnsActionFailure()
    {
        var actor = _sys.ActorOf(ProcessActionActor.Props(_treeProvider));
        var result = await actor.Ask<ActionFailure>(
            new KillProcess(-1), TimeSpan.FromSeconds(3));

        Assert.NotEmpty(result.Error);
    }
}
