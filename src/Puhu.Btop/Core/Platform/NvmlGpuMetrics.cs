using System.Runtime.InteropServices;
using System.Text;
using Puhu.Btop.Core.Models;

namespace Puhu.Btop.Core.Platform;

public sealed class NvmlGpuMetrics : IGpuMetrics
{
    private readonly nint _device;
    private readonly nint _lib;
    private readonly string _name = "NVIDIA GPU";

    private delegate int NvmlInitDelegate();
    private delegate int NvmlShutdownDelegate();
    private delegate int NvmlDeviceGetHandleByIndexDelegate(uint index, out nint device);
    private delegate int NvmlDeviceGetNameDelegate(nint device, byte[] name, uint length);
    private delegate int NvmlDeviceGetUtilizationRatesDelegate(nint device, out NvmlUtilization utilization);
    private delegate int NvmlDeviceGetMemoryInfoDelegate(nint device, out NvmlMemory memory);
    private delegate int NvmlDeviceGetTemperatureDelegate(nint device, int sensorType, out uint temperature);

    private readonly NvmlShutdownDelegate? _nvmlShutdown;
    private readonly NvmlDeviceGetUtilizationRatesDelegate? _nvmlGetUtilization;
    private readonly NvmlDeviceGetMemoryInfoDelegate? _nvmlGetMemory;
    private readonly NvmlDeviceGetTemperatureDelegate? _nvmlGetTemperature;

    public bool IsAvailable { get; }

    public NvmlGpuMetrics()
    {
        try
        {
            var libName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "nvml.dll"
                : "libnvidia-ml.so.1";

            if (!NativeLibrary.TryLoad(libName, out _lib))
            {
                IsAvailable = false;
                return;
            }

            var init = GetDelegate<NvmlInitDelegate>("nvmlInit_v2");
            _nvmlShutdown = GetDelegate<NvmlShutdownDelegate>("nvmlShutdown");
            var getHandle = GetDelegate<NvmlDeviceGetHandleByIndexDelegate>("nvmlDeviceGetHandleByIndex_v2");
            var getName = GetDelegate<NvmlDeviceGetNameDelegate>("nvmlDeviceGetName");
            _nvmlGetUtilization = GetDelegate<NvmlDeviceGetUtilizationRatesDelegate>("nvmlDeviceGetUtilizationRates");
            _nvmlGetMemory = GetDelegate<NvmlDeviceGetMemoryInfoDelegate>("nvmlDeviceGetMemoryInfo");
            _nvmlGetTemperature = GetDelegate<NvmlDeviceGetTemperatureDelegate>("nvmlDeviceGetTemperature");

            if (init is null || _nvmlShutdown is null || getHandle is null || init() != 0)
            {
                IsAvailable = false;
                return;
            }

            if (getHandle(0, out _device) != 0)
            {
                _nvmlShutdown();
                IsAvailable = false;
                return;
            }

            if (getName is not null)
            {
                var nameBuffer = new byte[64];
                if (getName(_device, nameBuffer, (uint)nameBuffer.Length) == 0)
                {
                    _name = Encoding.UTF8.GetString(nameBuffer).TrimEnd('\0');
                }
            }

            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    private T? GetDelegate<T>(string entryPoint) where T : Delegate
    {
        if (NativeLibrary.TryGetExport(_lib, entryPoint, out var ptr))
        {
            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }

        return null;
    }

    public GpuSnapshot GetSnapshot()
    {
        if (!IsAvailable)
        {
            return new GpuSnapshot(_name, 0, 0, 0, 0);
        }

        double usage = 0;
        ulong vramUsed = 0, vramTotal = 0;
        double temp = 0;

        try
        {
            if (_nvmlGetUtilization is not null &&
                _nvmlGetUtilization(_device, out var utilization) == 0)
            {
                usage = utilization.gpu;
            }
        }
        catch
        {
            // noop
        }

        try
        {
            if (_nvmlGetMemory is not null &&
                _nvmlGetMemory(_device, out var memInfo) == 0)
            {
                vramTotal = memInfo.total;
                vramUsed = memInfo.used;
            }
        }
        catch
        {
            // noop
        }

        try
        {
            if (_nvmlGetTemperature is not null &&
                _nvmlGetTemperature(_device, 0, out var temperature) == 0)
            {
                temp = temperature;
            }
        }
        catch
        {
            // noop
        }

        return new GpuSnapshot(_name, usage, vramUsed, vramTotal, temp);
    }

    ~NvmlGpuMetrics()
    {
        if (IsAvailable)
        {
            try { _nvmlShutdown?.Invoke(); }
            catch
            {
                // noop
            }
        }
        if (_lib != nint.Zero)
        {
            try { NativeLibrary.Free(_lib); }
            catch
            {
                // noop
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlUtilization
    {
        public uint gpu;
        public uint memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlMemory
    {
        public ulong total;
        public ulong free;
        public ulong used;
    }
}
