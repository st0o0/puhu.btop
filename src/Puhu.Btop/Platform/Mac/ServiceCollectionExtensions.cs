using Puhu.Btop.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Puhu.Btop.Platform.Mac;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMacPlatform(this IServiceCollection services)
    {
        services.AddSingleton<ICpuMetrics, MacCpuMetrics>();
        services.AddSingleton<IMemoryMetrics, MacMemoryMetrics>();
        services.AddSingleton<IDiskMetrics, MacDiskMetrics>();
        services.AddSingleton<INetworkMetrics, MacNetworkMetrics>();
        services.AddSingleton<IProcessClassifier, MacProcessClassifier>();
        services.AddSingleton<IProcessTreeProvider, MacProcessTree>();
        services.AddSingleton<IConnectionProvider, MacConnectionProvider>();
        return services;
    }
}
