using Puhu.Btop.Core.Messages;

namespace Puhu.Btop.Core.Platform;

public static class ProcessTreeBuilder
{
    private const int MaxDepth = 5;

    public static Dictionary<int, List<int>> BuildChildrenMap(Dictionary<int, int> parentMap)
    {
        var childrenMap = new Dictionary<int, List<int>>();
        foreach (var (pid, ppid) in parentMap)
        {
            if (!childrenMap.TryGetValue(ppid, out var children))
            {
                children = [];
                childrenMap[ppid] = children;
            }
            children.Add(pid);
        }
        return childrenMap;
    }

    public static ProcessTreeResult Build(int rootPid,
        Dictionary<int, int> parentMap, Dictionary<int, string> nameMap)
    {
        var childrenMap = BuildChildrenMap(parentMap);
        return BuildNode(rootPid, nameMap, childrenMap, 0);
    }

    private static ProcessTreeResult BuildNode(int pid, Dictionary<int, string> names,
        Dictionary<int, List<int>> childrenMap, int depth)
    {
        var name = names.GetValueOrDefault(pid, $"PID {pid}");
        var children = new List<ProcessTreeResult>();

        if (depth < MaxDepth && childrenMap.TryGetValue(pid, out var childPids))
        {
            foreach (var childPid in childPids.OrderBy(p => p))
            {
                children.Add(BuildNode(childPid, names, childrenMap, depth + 1));
            }
        }

        return new ProcessTreeResult(pid, name, children);
    }
}
