using Puhu.Btop.Core.Models;

namespace Puhu.Btop.Core.Platform;

public interface INetworkMetrics
{
    IReadOnlyList<NetworkSnapshot> Measure();
}
