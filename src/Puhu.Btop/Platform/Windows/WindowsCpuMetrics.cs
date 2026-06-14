using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Puhu.Btop.Core.Platform;
using Microsoft.Win32;

namespace Puhu.Btop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsCpuMetrics : ICpuMetrics
{
    private CpuCalculator.State _state;
    private string? _cpuName;

    public string ProcessorName => _cpuName ??= ReadCpuName();
    public int CoreCount => Environment.ProcessorCount;

    public CpuMeasurement Measure()
    {
        try
        {
            GetSystemTimes(out var idleTime, out var kernelTime, out var userTime);
            var idle = idleTime.ToLong();
            var total = kernelTime.ToLong() + userTime.ToLong();

            var coreCount = Environment.ProcessorCount;
            var (coreIdle, coreTotal) = ReadPerCoreRaw(coreCount);

            if (_state.CoreIdle is null)
            {
                _state = CpuCalculator.State.Initial(coreCount);
            }

            var result = CpuCalculator.Calculate(idle, total, coreIdle, coreTotal, _state);
            _state = result.NextState;
            return result.Measurement;
        }
        catch
        {
            return new CpuMeasurement(0, []);
        }
    }

    private static (long[] CoreIdle, long[] CoreTotal) ReadPerCoreRaw(int coreCount)
    {
        var size = Marshal.SizeOf<SystemProcessorPerformanceInformation>() * coreCount;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var status = NtQuerySystemInformation(8, buffer, size, out _);
            if (status != 0)
            {
                return (new long[coreCount], new long[coreCount]);
            }

            var coreIdle = new long[coreCount];
            var coreTotal = new long[coreCount];
            for (var i = 0; i < coreCount; i++)
            {
                var ptr = buffer + i * Marshal.SizeOf<SystemProcessorPerformanceInformation>();
                var info = Marshal.PtrToStructure<SystemProcessorPerformanceInformation>(ptr);
                coreIdle[i] = info.IdleTime;
                coreTotal[i] = info.KernelTime + info.UserTime;
            }
            return (coreIdle, coreTotal);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static string ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "CPU";
        }
        catch { return "CPU"; }
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out Filetime idle, out Filetime kernel, out Filetime user);

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr buffer, int size, out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemProcessorPerformanceInformation
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public int InterruptCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Filetime
    {
        public uint Low;
        public uint High;
        public long ToLong() => ((long)High << 32) | Low;
    }
}
