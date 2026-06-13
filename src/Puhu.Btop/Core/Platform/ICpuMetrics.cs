namespace Puhu.Btop.Core.Platform;

public interface ICpuMetrics
{
    string ProcessorName { get; }
    int CoreCount { get; }
    CpuMeasurement Measure();
}

public record CpuMeasurement(double TotalPercent, IReadOnlyList<double> CorePercents);
