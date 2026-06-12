using System.Runtime.InteropServices;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Mac;

public sealed class MacCpuMetrics : ICpuMetrics
{
    private const int PROCESSOR_CPU_LOAD_INFO = 2;
    private const int CPU_STATE_USER = 0;
    private const int CPU_STATE_SYSTEM = 1;
    private const int CPU_STATE_IDLE = 2;
    private const int CPU_STATE_NICE = 3;
    private const int CPU_STATE_MAX = 4;

    private CpuCalculator.State _state;

    public string ProcessorName => field ??= ReadCpuName();
    public int CoreCount => Environment.ProcessorCount;

    public CpuMeasurement Measure()
    {
        try
        {
            var host = mach_host_self();
            if (host_processor_info(host, PROCESSOR_CPU_LOAD_INFO, out var cpuCount,
                out var cpuInfo, out var cpuInfoCount) != 0)
                return new CpuMeasurement(0, []);

            var coreCount = (int)cpuCount;
            var coreIdle = new long[coreCount];
            var coreTotal = new long[coreCount];
            long totalIdle = 0, totalAll = 0;

            unsafe
            {
                var info = (int*)cpuInfo;
                for (var i = 0; i < coreCount; i++)
                {
                    var offset = i * CPU_STATE_MAX;
                    long user = info[offset + CPU_STATE_USER];
                    long system = info[offset + CPU_STATE_SYSTEM];
                    long idle = info[offset + CPU_STATE_IDLE];
                    long nice = info[offset + CPU_STATE_NICE];

                    coreIdle[i] = idle;
                    coreTotal[i] = user + system + idle + nice;
                    totalIdle += idle;
                    totalAll += coreTotal[i];
                }
            }

            vm_deallocate(mach_task_self(), cpuInfo, (nint)(cpuInfoCount * sizeof(int)));

            if (_state.CoreIdle is null)
                _state = CpuCalculator.State.Initial(coreCount);

            var result = CpuCalculator.Calculate(totalIdle, totalAll, coreIdle, coreTotal, _state);
            _state = result.NextState;
            return result.Measurement;
        }
        catch
        {
            return new CpuMeasurement(0, []);
        }
    }

    private static string ReadCpuName()
    {
        try
        {
            return RunSysctl("machdep.cpu.brand_string") ?? "CPU";
        }
        catch { return "CPU"; }
    }

    internal static string? RunSysctl(string key)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("sysctl", $"-n {key}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd().Trim();
            proc?.WaitForExit(3000);
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch { return null; }
    }

    [DllImport("libSystem.dylib")]
    private static extern uint mach_host_self();

    [DllImport("libSystem.dylib")]
    private static extern uint mach_task_self();

    [DllImport("libSystem.dylib")]
    private static extern int host_processor_info(uint host, int flavor,
        out uint processorCount, out nint processorInfo, out uint processorInfoCount);

    [DllImport("libSystem.dylib")]
    private static extern int vm_deallocate(uint task, nint address, nint size);
}
