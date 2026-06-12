using System.Runtime.Versioning;
using Akka.Actor;
using Microsoft.Extensions.DependencyInjection;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Pages;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop;

public sealed class BtopPlugin : IPuhuPlugin
{
    public string Name => "Btop";

    public void Configure(IPuhuPluginBuilder builder)
    {
        builder
            .WithTab("btop", "/btop")
            .WithServices(services =>
            {
                RegisterPlatform(services);

                var store = new MetricStore();
                services.AddSingleton(store);
                services.AddSingleton<IMetricSink>(store);
                services.AddSingleton<IMonitorDemand, MonitorDemandService>();
            })
            .WithActors((system, registry, resolver) =>
            {
                var diskMetrics = resolver.GetService<IDiskMetrics>();
                try { diskMetrics.Initialize(); }
                catch { /* degraded: usage only, no I/O rates */ }

                var supervisor = system.ActorOf(
                    MonitoringSupervisor.Props(
                        resolver.GetService<ICpuMetrics>(),
                        resolver.GetService<IMemoryMetrics>(),
                        diskMetrics,
                        resolver.GetService<INetworkMetrics>(),
                        resolver.GetService<IGpuMetrics>(),
                        resolver.GetService<IProcessClassifier>(),
                        resolver.GetService<IProcessTreeProvider>(),
                        resolver.GetService<IConnectionProvider>(),
                        resolver.GetService<IMetricSink>()),
                    "btop-monitoring");
                registry.Register<MonitoringSupervisor>(supervisor);

                // Bridge to host TickRouter: AlwaysOn — the supervisor does its own
                // demand gating internally for its child monitors.
                registry.Get<TickRouterKey>().Tell(
                    new Puhu.Plugin.RegisterMonitor("btop", supervisor, AlwaysOn: true, MinInterval: null));
            })
            .WithRoutes(termina =>
                termina.RegisterRoute<BtopPage, BtopViewModel>("/btop"));
    }

    private static void RegisterPlatform(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
            AddWindowsPlatform(services);
        else if (OperatingSystem.IsLinux())
            Platform.Linux.ServiceCollectionExtensions.AddLinuxPlatform(services);
        else if (OperatingSystem.IsMacOS())
            Platform.Mac.ServiceCollectionExtensions.AddMacPlatform(services);

        RegisterGpu(services);
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsPlatform(IServiceCollection services) =>
        Platform.Windows.ServiceCollectionExtensions.AddWindowsPlatform(services);

    private static void RegisterGpu(IServiceCollection services)
    {
        IGpuMetrics gpu = NoGpuMetrics.Instance;
        try
        {
            var nvml = new NvmlGpuMetrics();
            if (nvml.IsAvailable) gpu = nvml;
        }
        catch { /* no NVIDIA GPU — no GPU box */ }

        if (!gpu.IsAvailable && OperatingSystem.IsMacOS())
        {
            try { gpu = new Platform.Mac.MacGpuMetrics(); }
            catch { /* no Apple GPU box */ }
        }

        services.AddSingleton(gpu);
    }
}
