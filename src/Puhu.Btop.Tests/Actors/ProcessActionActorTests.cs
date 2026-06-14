using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Tests.Actors;

public sealed class ProcessActionActorTests : TestKit
{
    private readonly IProcessTreeProvider _treeProvider = Substitute.For<IProcessTreeProvider>();

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services) =>
        services.AddSingleton(_treeProvider);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry, resolver) =>
            registry.Register<ProcessActionActor>(system.ActorOf(resolver.Props<ProcessActionActor>(), "process-action")));

    [Fact(Timeout = 30000)]
    public async Task GetProcessTree_ReturnsTreeResult()
    {
        var expected = new ProcessTreeResult(1234, "test.exe", []);
        _treeProvider.BuildTree(1234).Returns(expected);

        var actor = ActorRegistry.Get<ProcessActionActor>();
        var result = await actor.Ask<ProcessTreeResult>(
            new GetProcessTree(1234), RemainingOrDefault, TestContext.Current.CancellationToken);

        Assert.Equal(1234, result.Pid);
        Assert.Equal("test.exe", result.Name);
    }

    [Fact(Timeout = 30000)]
    public async Task KillProcess_WithInvalidPid_ReturnsActionFailure()
    {
        var actor = ActorRegistry.Get<ProcessActionActor>();
        var result = await actor.Ask<ActionFailure>(
            new KillProcess(-1), RemainingOrDefault, TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Error);
    }
}
