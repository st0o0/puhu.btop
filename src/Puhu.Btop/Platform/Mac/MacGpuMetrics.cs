using System.Diagnostics;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Mac;

public sealed class MacGpuMetrics : IGpuMetrics
{
    private string? _gpuName;
    private ulong _vramTotal;
    private bool _initialized;

    public bool IsAvailable => true; // Apple Silicon always has GPU

    public GpuSnapshot GetSnapshot()
    {
        if (!_initialized)
        {
            Initialize();
        }

        // macOS doesn't expose GPU utilization without root
        return new GpuSnapshot(_gpuName ?? "Apple GPU", 0, 0, _vramTotal, 0);
    }

    private void Initialize()
    {
        _initialized = true;
        try
        {
            var psi = new ProcessStartInfo("system_profiler", "SPDisplaysDataType")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(5000);

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Chipset Model:"))
                {
                    _gpuName = trimmed.Split(':')[1].Trim();
                }
                else if (trimmed.StartsWith("VRAM") && trimmed.Contains("MB"))
                {
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts)
                    {
                        if (ulong.TryParse(p, out var mb))
                        {
                            _vramTotal = mb * 1024 * 1024;
                        }
                    }
                }
            }
            _gpuName ??= "Apple GPU";
        }
        catch
        {
            _gpuName = "Apple GPU";
        }
    }
}
