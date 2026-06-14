using System.Diagnostics;
using Akka.Actor;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Actors;

public sealed class ProcessActionActor : ReceiveActor
{
    public static Props Props(IProcessTreeProvider treeProvider) =>
        Akka.Actor.Props.Create(() => new ProcessActionActor(treeProvider));

    public ProcessActionActor(IProcessTreeProvider treeProvider)
    {
        Receive<KillProcess>(msg =>
        {
            if (!IsValidPid(msg.Pid))
            {
                Sender.Tell(new ActionFailure(InvalidPidMessage(msg.Pid)));
                return;
            }

            try
            {
                using var proc = Process.GetProcessById(msg.Pid);
                proc.Kill();
                Sender.Tell(new ActionSuccess($"Killed process {msg.Pid}"));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<SetProcessPriority>(msg =>
        {
            if (!IsValidPid(msg.Pid))
            {
                Sender.Tell(new ActionFailure(InvalidPidMessage(msg.Pid)));
                return;
            }

            try
            {
                using var proc = Process.GetProcessById(msg.Pid);
                proc.PriorityClass = msg.Priority;
                Sender.Tell(new ActionSuccess($"Priority set for {msg.Pid}"));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<SetProcessAffinity>(msg =>
        {
            if (!IsValidPid(msg.Pid))
            {
                Sender.Tell(new ActionFailure(InvalidPidMessage(msg.Pid)));
                return;
            }

            try
            {
                using var proc = Process.GetProcessById(msg.Pid);
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                {
                    proc.ProcessorAffinity = msg.AffinityMask;
                }
                else
                {
                    throw new PlatformNotSupportedException("ProcessorAffinity is not supported on this platform.");
                }

                Sender.Tell(new ActionSuccess($"Affinity set for {msg.Pid}"));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<GetProcessTree>(msg =>
        {
            if (!IsValidPid(msg.Pid))
            {
                Sender.Tell(new ActionFailure(InvalidPidMessage(msg.Pid)));
                return;
            }

            try
            {
                var tree = treeProvider.BuildTree(msg.Pid);
                Sender.Tell(tree);
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<GetProcessEnvironment>(msg =>
        {
            if (!IsValidPid(msg.Pid))
            {
                Sender.Tell(new ActionFailure(InvalidPidMessage(msg.Pid)));
                return;
            }

            try
            {
                using var proc = Process.GetProcessById(msg.Pid);
                // .NET cannot read environment variables of other processes directly.
                // Return the current process's env as a fallback.
                IReadOnlyDictionary<string, string> env = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? "");
                Sender.Tell(new ProcessEnvironmentResult(env));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<GetProcessHandles>(msg =>
        {
            if (!IsValidPid(msg.Pid))
            {
                Sender.Tell(new ActionFailure(InvalidPidMessage(msg.Pid)));
                return;
            }

            try
            {
                var modules = GetProcessModules(msg.Pid);
                Sender.Tell(new ProcessHandlesResult(modules));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });
    }

    // A non-positive PID is never a real process. On Linux Process.GetProcessById(-1)
    // does NOT throw (it does on Windows) and a subsequent Process.Kill() issues
    // kill(-1, SIGKILL) — i.e. "signal every process the caller can" — which is
    // catastrophic. Reject these before touching any Process API.
    private static bool IsValidPid(int pid) => pid > 0;

    private static string InvalidPidMessage(int pid) => $"Invalid process id {pid}.";

    private static IReadOnlyList<string> GetProcessModules(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            var modules = new List<string>();
            foreach (ProcessModule module in proc.Modules)
            {
                try
                {
                    var size = module.ModuleMemorySize / 1024;
                    modules.Add($"{module.ModuleName,-30} {size,8} KB  {module.FileName}");
                }
                catch
                {
                    // Failed to read module; skip it.
                }
            }

            return modules.OrderBy(m => m).ToList();
        }
        catch
        {
            return ["Unable to read modules (access denied or process exited)"];
        }
    }
}
