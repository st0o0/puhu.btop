using Puhu.Btop.Core.Messages;

namespace Puhu.Btop.Core.Platform;

public interface IProcessTreeProvider
{
    ProcessTreeResult BuildTree(int rootPid);

    /// <summary>
    /// Returns a pid → parent-pid map for all currently visible processes.
    /// Used to populate <see cref="Core.Models.ProcessSnapshot.ParentPid"/> so the
    /// process list can be rendered as a tree.
    /// </summary>
    IReadOnlyDictionary<int, int> ReadParentMap();
}
