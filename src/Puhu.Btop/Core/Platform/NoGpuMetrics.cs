using Puhu.Btop.Core.Models;

namespace Puhu.Btop.Core.Platform;

public sealed class NoGpuMetrics : IGpuMetrics
{
    public static readonly NoGpuMetrics Instance = new();
    public bool IsAvailable => false;
    public GpuSnapshot GetSnapshot() => new("N/A", 0, 0, 0, 0);
}
