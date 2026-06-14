using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Puhu.Btop.Actors;

namespace Puhu.Btop.Tests.Actors;

/// <summary>
/// Verifies the in-house <see cref="ActorContextExtensions.ResolveChildActor{TActor}"/> — the
/// replacement for the external Servus.Akka extension the supervisors used to depend on — creates
/// a child via the host DI <see cref="DependencyResolver"/> with its constructor dependencies
/// injected, under the requested name.
/// </summary>
public sealed class ResolveChildActorTests : TestKit
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Injected
    {
        public string Value => "from-di";
    }

    private sealed class Child : ReceiveActor
    {
        public Child(Injected injected) => Receive<string>(_ => Sender.Tell(injected.Value));
    }

    private sealed class Parent : ReceiveActor
    {
        public Parent()
        {
            var child = Context.ResolveChildActor<Child>("child");
            Receive<string>(msg => child.Forward(msg));
        }
    }

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services) =>
        services.AddSingleton<Injected>();

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        // Actors are created per-test; nothing to register at startup.
    }

    [Fact(Timeout = 30000)]
    public async Task ResolveChildActor_creates_child_with_DI_injected_dependencies()
    {
        var parent = Sys.ActorOf(DependencyResolver.For(Sys).Props<Parent>(), "parent");

        parent.Tell("ping");

        var reply = await ExpectMsgAsync<string>(cancellationToken: Ct);
        Assert.Equal("from-di", reply);
    }

    [Fact(Timeout = 30000)]
    public async Task ResolveChildActor_creates_child_under_the_requested_name()
    {
        var parent = Sys.ActorOf(DependencyResolver.For(Sys).Props<Parent>(), "named-parent");

        // Round-trip first so the parent (and thus its child) is fully constructed.
        parent.Tell("ping");
        await ExpectMsgAsync<string>(cancellationToken: Ct);

        var child = await Sys
            .ActorSelection("/user/named-parent/child")
            .ResolveOne(TimeSpan.FromSeconds(5), Ct);

        Assert.Equal("child", child.Path.Name);
    }
}
