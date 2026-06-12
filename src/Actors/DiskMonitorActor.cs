using Akka.Actor;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Services;
using Puhu.Plugin;

namespace Puhu.Btop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class DiskMonitorActor : ReceiveActor
{
    public static Props Props(IDiskMetrics diskMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new DiskMonitorActor(diskMetrics, sink));

    public DiskMonitorActor(IDiskMetrics diskMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            var disks = DriveInfo.GetDrives()
                .Where(d => d is { IsReady: true, TotalSize: > 0 })
                .Select(d =>
                {
                    var name = d.Name.TrimEnd('\\', '/');
                    if (name is [_, ':', ..])
                    {
                        name = name[..2];
                    }

                    var (read, write, active) = diskMetrics.GetMetrics(name);
                    return new DiskSnapshot(name, (ulong)d.TotalSize, (ulong)d.AvailableFreeSpace, read, write, active);
                })
                .OrderBy(d => d.Name)
                .ToList();
            sink.Publish(disks);
        });
    }
}
