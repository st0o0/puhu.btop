using System.Net;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Linux;

public sealed class LinuxConnectionProvider : IConnectionProvider
{
    public List<ConnectionSnapshot> GetConnections()
    {
        var results = new List<ConnectionSnapshot>();
        var inodeToProcess = BuildInodeMap();

        try
        {
            results.AddRange(ParseProcNet("/proc/net/tcp", "TCP", inodeToProcess));
        }
        catch
        {
        }

        try
        {
            results.AddRange(ParseProcNet("/proc/net/udp", "UDP", inodeToProcess));
        }
        catch
        {
        }

        return results;
    }

    private static Dictionary<long, (int Pid, string Name)> BuildInodeMap()
    {
        var map = new Dictionary<long, (int, string)>();
        try
        {
            foreach (var procDir in Directory.GetDirectories("/proc"))
            {
                var dirName = Path.GetFileName(procDir);
                if (!int.TryParse(dirName, out var pid))
                {
                    continue;
                }

                var fdDir = Path.Combine(procDir, "fd");
                if (!Directory.Exists(fdDir))
                {
                    continue;
                }

                string processName;
                try
                {
                    var comm = File.ReadAllText(Path.Combine(procDir, "comm")).Trim();
                    processName = comm;
                }
                catch
                {
                    continue;
                }

                try
                {
                    foreach (var fd in Directory.GetFiles(fdDir))
                    {
                        try
                        {
                            var link = File.ResolveLinkTarget(fd, false)?.ToString() ?? "";
                            if (link.StartsWith("socket:[") && link.EndsWith(']'))
                            {
                                var inodeStr = link["socket:[".Length..^1];
                                if (long.TryParse(inodeStr, out var inode))
                                {
                                    map.TryAdd(inode, (pid, processName));
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return map;
    }

    private static List<ConnectionSnapshot> ParseProcNet(string path, string protocol,
        Dictionary<long, (int Pid, string Name)> inodeMap)
    {
        var results = new List<ConnectionSnapshot>();
        if (!File.Exists(path))
        {
            return results;
        }

        var lines = File.ReadAllLines(path);
        foreach (var line in lines.Skip(1)) // skip header
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 10)
            {
                continue;
            }

            try
            {
                var localEp = ParseHexEndpoint(parts[1]);
                var remoteEp = ParseHexEndpoint(parts[2]);
                var stateHex = Convert.ToInt32(parts[3], 16);
                var state = protocol == "TCP" ? MapTcpState(stateHex) : "LISTEN";
                var inode = long.Parse(parts[9]);

                var (pid, name) = inodeMap.GetValueOrDefault(inode, (0, ""));
                results.Add(new ConnectionSnapshot(name, pid, localEp, remoteEp, state, protocol));
            }
            catch
            {
            }
        }

        return results;
    }

    private static string ParseHexEndpoint(string hex)
    {
        var parts = hex.Split(':');
        if (parts.Length != 2)
        {
            return hex;
        }

        var ipBytes = Convert.FromHexString(parts[0]);
        // /proc/net uses network byte order on little-endian
        if (BitConverter.IsLittleEndian && ipBytes.Length == 4)
        {
            Array.Reverse(ipBytes);
        }

        var ip = new IPAddress(ipBytes);
        var port = Convert.ToInt32(parts[1], 16);
        return $"{ip}:{port}";
    }

    private static string MapTcpState(int state) => state switch
    {
        1 => "Established",
        2 => "SynSent",
        3 => "SynReceived",
        4 => "FinWait1",
        5 => "FinWait2",
        6 => "TimeWait",
        7 => "Closed",
        8 => "CloseWait",
        9 => "LastAck",
        10 => "LISTEN",
        11 => "Closing",
        _ => "Unknown"
    };
}
