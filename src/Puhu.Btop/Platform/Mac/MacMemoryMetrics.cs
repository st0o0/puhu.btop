using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Mac;

public sealed class MacMemoryMetrics : IMemoryMetrics
{
    public (ulong TotalBytes, ulong UsedBytes) Measure()
    {
        try
        {
            var totalStr = MacCpuMetrics.RunSysctl("hw.memsize");
            if (totalStr is null || !ulong.TryParse(totalStr, out var total))
            {
                return (0, 0);
            }

            // Parse vm_stat for memory breakdown
            var psi = new System.Diagnostics.ProcessStartInfo("vm_stat")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(3000);

            // vm_stat output has page size on first line, then "Pages X: N."
            var pageSize = 16384UL; // default on Apple Silicon
            ulong active = 0, wired = 0, compressed = 0;

            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("Mach Virtual Memory Statistics"))
                {
                    // Extract page size from "page size of XXXXX bytes"
                    var parts = line.Split(' ');
                    for (var i = 0; i < parts.Length - 1; i++)
                        if (parts[i] == "of" && ulong.TryParse(parts[i + 1], out var ps))
                        {
                            pageSize = ps;
                        }
                }
                else if (line.Contains("Pages active:"))
                {
                    active = ParseVmStatValue(line);
                }
                else if (line.Contains("Pages wired"))
                {
                    wired = ParseVmStatValue(line);
                }
                else if (line.Contains("Pages occupied by compressor"))
                {
                    compressed = ParseVmStatValue(line);
                }
            }

            var used = (active + wired + compressed) * pageSize;
            return (total, Math.Min(used, total));
        }
        catch
        {
            return (0, 0);
        }
    }

    private static ulong ParseVmStatValue(string line)
    {
        // Format: "Pages active:                   123456."
        var colon = line.IndexOf(':');
        if (colon < 0)
        {
            return 0;
        }

        var numStr = line[(colon + 1)..].Trim().TrimEnd('.');
        return ulong.TryParse(numStr, out var val) ? val : 0;
    }
}
