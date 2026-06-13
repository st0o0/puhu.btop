using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessTree : IProcessTreeProvider
{
    public ProcessTreeResult BuildTree(int rootPid)
    {
        var (parentMap, nameMap) = ReadProcessMaps();
        return ProcessTreeBuilder.Build(rootPid, parentMap, nameMap);
    }

    private static (Dictionary<int, int> ParentMap, Dictionary<int, string> NameMap) ReadProcessMaps()
    {
        var parentMap = new Dictionary<int, int>();
        var nameMap = new Dictionary<int, string>();

        var snapshot = CreateToolhelp32Snapshot(Th32CsSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            foreach (var p in Process.GetProcesses())
            {
                try { nameMap[p.Id] = p.ProcessName; } catch { /* exited */ }
            }
            return (parentMap, nameMap);
        }

        try
        {
            var entry = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (Process32FirstW(snapshot, ref entry))
            {
                do
                {
                    var pid = (int)entry.th32ProcessID;
                    parentMap[pid] = (int)entry.th32ParentProcessID;
                    nameMap[pid] = entry.szExeFile;
                } while (Process32NextW(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return (parentMap, nameMap);
    }

    private const uint Th32CsSnapProcess = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32NextW(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
