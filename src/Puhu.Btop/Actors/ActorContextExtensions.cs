using Akka.Actor;
using Akka.DependencyInjection;

namespace Puhu.Btop.Actors;

/// <summary>
/// Resolves a child actor through Akka's DI <see cref="DependencyResolver"/> so each child's
/// constructor receives its services directly. This mirrors the external Servus.Akka extension
/// of the same name, but keeps the plugin to a single shipped DLL with no dependency the host
/// does not already provide — <c>Akka.DependencyInjection</c> ships with the host via Akka.Hosting.
/// </summary>
internal static class ActorContextExtensions
{
    public static IActorRef ResolveChildActor<TActor>(this IActorContext context, string name)
        where TActor : ActorBase =>
        context.ActorOf(DependencyResolver.For(context.System).Props<TActor>(), name);
}
