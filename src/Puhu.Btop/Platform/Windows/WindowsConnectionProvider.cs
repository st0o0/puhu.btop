using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsConnectionProvider : IConnectionProvider
{
    private const int AfInet = 2;
    private const int TcpTableOwnerPidAll = 5;
    private const int UdpTableOwnerPid = 1;

    public List<ConnectionSnapshot> GetConnections()
    {
        var results = new List<ConnectionSnapshot>();
        var processNames = new Dictionary<int, string>();

        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try { processNames[p.Id] = p.ProcessName; }
                catch { }
                p.Dispose();
            }
        }
        catch { }

        try { results.AddRange(GetTcpConnections(processNames)); } catch { }
        try { results.AddRange(GetUdpEndpoints(processNames)); } catch { }

        return results;
    }

    private static List<ConnectionSnapshot> GetTcpConnections(Dictionary<int, string> processNames)
    {
        var results = new List<ConnectionSnapshot>();
        var size = 0;
        GetExtendedTcpTable(nint.Zero, ref size, true, AfInet, TcpTableOwnerPidAll, 0);

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, true, AfInet, TcpTableOwnerPidAll, 0) != 0)
            {
                return results;
            }

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPtr = buffer + 4;
            var rowSize = Marshal.SizeOf<MibTcprowOwnerPid>();

            for (var i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MibTcprowOwnerPid>(rowPtr);
                var local = new IPEndPoint(row.dwLocalAddr, (ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort));
                var remote = new IPEndPoint(row.dwRemoteAddr, (ushort)IPAddress.NetworkToHostOrder((short)row.dwRemotePort));
                var state = MapTcpState(row.dwState);
                var name = processNames.GetValueOrDefault(row.dwOwningPid, "");

                results.Add(new ConnectionSnapshot(name, row.dwOwningPid, local.ToString(), remote.ToString(), state, "TCP"));
                rowPtr += rowSize;
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }

        return results;
    }

    private static List<ConnectionSnapshot> GetUdpEndpoints(Dictionary<int, string> processNames)
    {
        var results = new List<ConnectionSnapshot>();
        var size = 0;
        GetExtendedUdpTable(nint.Zero, ref size, true, AfInet, UdpTableOwnerPid, 0);

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedUdpTable(buffer, ref size, true, AfInet, UdpTableOwnerPid, 0) != 0)
            {
                return results;
            }

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPtr = buffer + 4;
            var rowSize = Marshal.SizeOf<MibUdprowOwnerPid>();

            for (var i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MibUdprowOwnerPid>(rowPtr);
                var local = new IPEndPoint(row.dwLocalAddr, (ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort));
                var name = processNames.GetValueOrDefault(row.dwOwningPid, "");

                results.Add(new ConnectionSnapshot(name, row.dwOwningPid, local.ToString(), "*:*", "LISTEN", "UDP"));
                rowPtr += rowSize;
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }

        return results;
    }

    private static string MapTcpState(int state) => state switch
    {
        1 => "Closed",
        2 => "LISTEN",
        3 => "SynSent",
        4 => "SynReceived",
        5 => "Established",
        6 => "FinWait1",
        7 => "FinWait2",
        8 => "CloseWait",
        9 => "Closing",
        10 => "LastAck",
        11 => "TimeWait",
        12 => "DeleteTCB",
        _ => "Unknown"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcprowOwnerPid
    {
        public int dwState;
        public uint dwLocalAddr;
        public int dwLocalPort;
        public uint dwRemoteAddr;
        public int dwRemotePort;
        public int dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdprowOwnerPid
    {
        public uint dwLocalAddr;
        public int dwLocalPort;
        public int dwOwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetExtendedTcpTable(nint pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, int reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetExtendedUdpTable(nint pUdpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, int reserved);
}
