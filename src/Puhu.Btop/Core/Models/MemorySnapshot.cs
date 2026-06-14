namespace Puhu.Btop.Core.Models;

public record MemorySnapshot(
    ulong TotalBytes,
    ulong UsedBytes)
{
    public double UsedPercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
}
