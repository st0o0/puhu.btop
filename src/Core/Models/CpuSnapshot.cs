namespace Puhu.Btop.Core.Models;

public record CpuSnapshot(
    string Name,
    double TotalPercent,
    IReadOnlyList<double> CorePercents);
