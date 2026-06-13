using Puhu.Btop.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Puhu.Btop.Platform.Linux;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLinuxPlatform(this IServiceCollection services)
    {
        services.AddSingleton<ICpuMetrics, LinuxCpuMetrics>();
        services.AddSingleton<IMemoryMetrics, LinuxMemoryMetrics>();
        services.AddSingleton<IDiskMetrics, LinuxDiskMetrics>();
        services.AddSingleton<INetworkMetrics, LinuxNetworkMetrics>();
        services.AddSingleton<IProcessClassifier, LinuxProcessClassifier>();
        services.AddSingleton<IProcessTreeProvider, LinuxProcessTree>();
        services.AddSingleton<IConnectionProvider, LinuxConnectionProvider>();
        return services;
    }
}
