namespace Puhu.Btop.Core.Messages;

public sealed record GetProcessTree(int Pid);
public sealed record GetProcessEnvironment(int Pid);
public sealed record GetProcessHandles(int Pid);

public sealed record ProcessTreeResult(
    int Pid, string Name, IReadOnlyList<ProcessTreeResult> Children);
public sealed record ProcessEnvironmentResult(
    IReadOnlyDictionary<string, string> Variables);
public sealed record ProcessHandlesResult(
    IReadOnlyList<string> Handles);
