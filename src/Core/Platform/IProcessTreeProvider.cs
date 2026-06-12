using Puhu.Btop.Core.Messages;

namespace Puhu.Btop.Core.Platform;

public interface IProcessTreeProvider
{
    ProcessTreeResult BuildTree(int rootPid);
}
