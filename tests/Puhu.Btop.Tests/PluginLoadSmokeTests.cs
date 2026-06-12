using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;
using Termina.Hosting;

namespace Puhu.Btop.Tests;

/// <summary>
/// Headless integration smoke test that exercises the plugin's public contract
/// end-to-end (short of the actor system), using the REAL host Puhu.Plugin types
/// from the pinned submodule. This guards the plugin wiring: tab/route registration
/// and DI service registration must succeed without throwing.
/// </summary>
public sealed class PluginLoadSmokeTests
{
    [Fact]
    public void Plugin_Name_Is_Btop()
    {
        var plugin = new BtopPlugin();
        Assert.Equal("Btop", plugin.Name);
    }

    [Fact]
    public void Plugin_Configure_Registers_Tab_Route_And_Services_Without_Throwing()
    {
        var plugin = new BtopPlugin();
        var builder = new RecordingPluginBuilder();

        // Should not throw — runs WithServices against a real ServiceCollection.
        plugin.Configure(builder);

        // Tab label + route captured.
        Assert.Equal("btop", builder.TabLabel);
        Assert.Equal("/btop", builder.TabRoute);

        // WithRoutes / WithActors delegates captured but not invoked
        // (they need a live TerminaBuilder / ActorSystem — out of scope here).
        Assert.NotNull(builder.RoutesConfigure);
        Assert.NotNull(builder.ActorsConfigure);

        // Service registrations executed against a real ServiceCollection.
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(MetricStore));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IMetricSink));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IMonitorDemand));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IGpuMetrics));

        // A platform ICpuMetrics is registered for the current OS.
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(ICpuMetrics));

        // The container can be built (registrations are internally consistent).
        using var provider = builder.Services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IMetricSink>());
        Assert.NotNull(provider.GetRequiredService<IGpuMetrics>());
    }

    /// <summary>
    /// In-test <see cref="IPuhuPluginBuilder"/> that records calls. WithServices is run
    /// against a real <see cref="ServiceCollection"/>; WithRoutes/WithActors delegates are
    /// captured but not invoked (TerminaBuilder has an internal ctor and the actor delegate
    /// requires a live ActorSystem/registry).
    /// </summary>
    private sealed class RecordingPluginBuilder : IPuhuPluginBuilder
    {
        public ServiceCollection Services { get; } = new();
        public string? TabLabel { get; private set; }
        public string? TabRoute { get; private set; }
        public string? SettingsLabel { get; private set; }
        public string? SettingsRoute { get; private set; }
        public Action<TerminaBuilder>? RoutesConfigure { get; private set; }
        public Action<ActorSystem, IActorRegistry, IDependencyResolver>? ActorsConfigure { get; private set; }

        public IPuhuPluginBuilder WithTab(string label, string route)
        {
            TabLabel = label;
            TabRoute = route;
            return this;
        }

        public IPuhuPluginBuilder WithSettings(string label, string route)
        {
            SettingsLabel = label;
            SettingsRoute = route;
            return this;
        }

        public IPuhuPluginBuilder WithServices(Action<IServiceCollection> configure)
        {
            configure(Services);
            return this;
        }

        public IPuhuPluginBuilder WithActors(Action<ActorSystem, IActorRegistry, IDependencyResolver> configure)
        {
            ActorsConfigure = configure;
            return this;
        }

        public IPuhuPluginBuilder WithRoutes(Action<TerminaBuilder> configure)
        {
            RoutesConfigure = configure;
            return this;
        }
    }
}
