namespace Puhu.Btop.Core.Messages;

/// <summary>Identifies a monitor for demand tracking.</summary>
public enum MetricKind
{
    Cpu,
    Memory,
    Gpu,
    Disk,
    Network,
    Process,
    NetworkConnections,
}
