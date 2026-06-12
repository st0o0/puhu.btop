namespace Puhu.Btop.Core.Models;

public record NetworkSnapshot(
    string Name,
    ulong RxBytesPerSec,
    ulong TxBytesPerSec);
