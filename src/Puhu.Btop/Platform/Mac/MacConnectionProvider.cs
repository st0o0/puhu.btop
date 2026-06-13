using System.Diagnostics;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Platform.Mac;

public sealed class MacConnectionProvider : IConnectionProvider
{
    public List<ConnectionSnapshot> GetConnections()
    {
        try
        {
            var psi = new ProcessStartInfo("lsof", "-i -n -P")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(5000);

            var connections = new List<ConnectionSnapshot>();
            foreach (var line in output.Split('\n').Skip(1)) // skip header
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 9)
                {
                    continue;
                }

                var processName = parts[0];
                _ = int.TryParse(parts[1], out var pid);
                var protocol = parts[7]; // TCP or UDP
                var nameField = parts[8]; // host:port->host:port (ESTABLISHED)

                var state = "";
                if (parts.Length > 9)
                {
                    state = parts[9].Trim('(', ')');
                }

                var arrow = nameField.IndexOf("->", StringComparison.Ordinal);
                string local, remote;
                if (arrow >= 0)
                {
                    local = nameField[..arrow];
                    remote = nameField[(arrow + 2)..];
                }
                else
                {
                    local = nameField;
                    remote = "";
                }

                connections.Add(new ConnectionSnapshot(processName, pid, local, remote, state, protocol));
            }

            return connections;
        }
        catch
        {
            return [];
        }
    }
}
