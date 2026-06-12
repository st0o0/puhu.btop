namespace Puhu.Btop.Core.Platform;

public interface IMemoryMetrics
{
    (ulong TotalBytes, ulong UsedBytes) Measure();
}
