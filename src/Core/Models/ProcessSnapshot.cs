namespace Puhu.Btop.Core.Models;

public enum ProcessGroup { Apps, Background, Windows }

public record ProcessSnapshot(
    int Pid,
    string Name,
    ProcessGroup Group,
    double CpuPercent,
    long WorkingSetBytes,
    long DiskBytesPerSec,
    long NetworkBytesPerSec,
    int ThreadCount,
    int HandleCount,
    string UserName,
    int ParentPid);
