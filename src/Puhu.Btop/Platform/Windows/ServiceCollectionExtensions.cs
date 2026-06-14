using System.Runtime.Versioning;
using Puhu.Btop.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Puhu.Btop.Platform.Windows;

public static class ServiceCollectionExtensions
{
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<ICpuMetrics, WindowsCpuMetrics>();
        services.AddSingleton<IMemoryMetrics, WindowsMemoryMetrics>();
        services.AddSingleton<IDiskMetrics, WindowsDiskMetrics>();
        services.AddSingleton<INetworkMetrics, WindowsNetworkMetrics>();
        services.AddSingleton<IProcessClassifier, WindowsProcessClassifier>();
        services.AddSingleton<IProcessTreeProvider, WindowsProcessTree>();
        services.AddSingleton<IConnectionProvider, WindowsConnectionProvider>();
        return services;
    }
}
