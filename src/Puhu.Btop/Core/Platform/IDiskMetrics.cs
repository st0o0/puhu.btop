namespace Puhu.Btop.Core.Platform;

public interface IDiskMetrics : IDisposable
{
    void Initialize();
    (ulong ReadBytesPerSec, ulong WriteBytesPerSec, double ActivePercent) GetMetrics(string diskName);
}
