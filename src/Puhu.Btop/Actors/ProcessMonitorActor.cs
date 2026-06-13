using System.Diagnostics;
using Akka.Actor;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

public sealed class ProcessMonitorActor : ReceiveActor
{
    private readonly IProcessClassifier _classifier;
    private readonly IProcessTreeProvider _treeProvider;
    private readonly IMetricSink _sink;
    private Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)> _previousCpu = new();

    public static Props Props(IProcessClassifier classifier, IProcessTreeProvider treeProvider, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new ProcessMonitorActor(classifier, treeProvider, sink));

    public ProcessMonitorActor(IProcessClassifier classifier, IProcessTreeProvider treeProvider, IMetricSink sink)
    {
        _classifier = classifier;
        _treeProvider = treeProvider;
        _sink = sink;

        Receive<Tick>(_ =>
        {
            var now = DateTime.UtcNow;
            var coreCount = Environment.ProcessorCount;
            var currentCpu = new Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)>();

            // pid → parent-pid for the whole system, so each snapshot carries its
            // ParentPid and the UI can render the process list as a tree ('e' key).
            var parentMap = ReadParentMap();

            var processes = Process.GetProcesses();
            try
            {
                var snapshots = processes
                    .Select(p =>
                    {
                        try
                        {
                            var pid = p.Id;
                            var cpuTime = p.TotalProcessorTime;
                            currentCpu[pid] = (cpuTime, now);

                            double cpuPercent = 0;
                            if (_previousCpu.TryGetValue(pid, out var prev))
                            {
                                var elapsed = (now - prev.Timestamp).TotalMilliseconds;
                                if (elapsed > 0)
                                {
                                    var cpuDelta = (cpuTime - prev.CpuTime).TotalMilliseconds;
                                    cpuPercent = cpuDelta / elapsed / coreCount * 100;
                                    cpuPercent = Math.Clamp(cpuPercent, 0, 100);
                                }
                            }

                            return new ProcessSnapshot(
                                Pid: pid, Name: p.ProcessName, Group: _classifier.Classify(p),
                                CpuPercent: Math.Round(cpuPercent, 1),
                                WorkingSetBytes: p.WorkingSet64,
                                DiskBytesPerSec: 0, NetworkBytesPerSec: 0,
                                ThreadCount: p.Threads.Count, HandleCount: p.HandleCount,
                                UserName: "", ParentPid: parentMap.GetValueOrDefault(pid, 0));
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(p => p is not null)
                    .OrderByDescending(p => p!.WorkingSetBytes)
                    .ToList();

                _previousCpu = currentCpu;
                _sink.Publish(snapshots!);
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }
        });
    }

    private IReadOnlyDictionary<int, int> ReadParentMap()
    {
        try
        {
            return _treeProvider.ReadParentMap();
        }
        catch
        {
            // Parent lookup is best-effort; without it the list just renders flat.
            return new Dictionary<int, int>();
        }
    }
}
