using System.Diagnostics;
using Puhu.Btop.Core.Models;

namespace Puhu.Btop.Core.Platform;

public interface IProcessClassifier
{
    ProcessGroup Classify(Process process);
}
