using System.Diagnostics;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Mac;

public sealed class MacProcessTree : IProcessTreeProvider
{
    public ProcessTreeResult BuildTree(int rootPid)
    {
        try
        {
            var (parentMap, nameMap) = ReadProcessMaps();
            return ProcessTreeBuilder.Build(rootPid, parentMap, nameMap);
        }
        catch
        {
            return new ProcessTreeResult(rootPid, $"PID {rootPid}", []);
        }
    }

    private static (Dictionary<int, int> ParentMap, Dictionary<int, string> NameMap) ReadProcessMaps()
    {
        var psi = new ProcessStartInfo("ps", "-eo pid,ppid,comm")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        var output = proc?.StandardOutput.ReadToEnd() ?? "";
        proc?.WaitForExit(3000);

        var parentMap = new Dictionary<int, int>();
        var nameMap = new Dictionary<int, string>();

        foreach (var line in output.Split('\n').Skip(1))
        {
            var parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && int.TryParse(parts[0], out var pid) && int.TryParse(parts[1], out var ppid))
            {
                parentMap[pid] = ppid;
                nameMap[pid] = Path.GetFileName(parts[2].Trim());
            }
        }

        return (parentMap, nameMap);
    }
}
