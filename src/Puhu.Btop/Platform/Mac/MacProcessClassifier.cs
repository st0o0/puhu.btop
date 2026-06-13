using System.Diagnostics;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Mac;

public sealed class MacProcessClassifier : IProcessClassifier
{
    public ProcessGroup Classify(Process process)
    {
        try
        {
            var name = process.ProcessName.ToLowerInvariant();

            // Kernel/system processes
            if (process.Id == 0 || name is "kernel_task" or "launchd" or "windowserver"
                or "mds" or "mds_stores" or "logd" or "syslogd" or "configd")
            {
                return ProcessGroup.Windows; // "System" group
            }

            // Background daemons
            if (name.EndsWith('d') && name.Length > 3 && !name.Contains('.'))
            {
                return ProcessGroup.Background;
            }

            if (name.StartsWith("com.apple."))
            {
                return ProcessGroup.Background;
            }

            return ProcessGroup.Apps;
        }
        catch
        {
            return ProcessGroup.Apps;
        }
    }
}
