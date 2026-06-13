namespace Puhu.Btop.Core.Models;

public record GpuSnapshot(
    string Name,
    double UsagePercent,
    ulong VramUsedBytes,
    ulong VramTotalBytes,
    double TemperatureCelsius)
{
    public double VramUsedPercent => VramTotalBytes > 0 ? (double)VramUsedBytes / VramTotalBytes * 100 : 0;
}
