using Puhu.Btop.Core.Models;

namespace Puhu.Btop.Core.Platform;

public interface IGpuMetrics
{
    bool IsAvailable { get; }
    GpuSnapshot GetSnapshot();
}
