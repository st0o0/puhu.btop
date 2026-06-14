using Puhu.Btop.Core.Models;

namespace Puhu.Btop.Core.Platform;

public interface IConnectionProvider
{
    List<ConnectionSnapshot> GetConnections();
}
