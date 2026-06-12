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
            try
            {
                using var proc = Process.GetProcessById(msg.Pid);
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                    proc.ProcessorAffinity = msg.AffinityMask;
                else
                    throw new PlatformNotSupportedException("ProcessorAffinity is not supported on this platform.");
                Sender.Tell(new ActionSuccess($"Affinity set for {msg.Pid}"));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<GetProcessTree>(msg =>
        {
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
