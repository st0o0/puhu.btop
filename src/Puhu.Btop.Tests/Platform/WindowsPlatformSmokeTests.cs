// Calls are runtime-guarded by Assert.SkipUnless(OperatingSystem.IsWindows()).
using System.Runtime.Versioning;
using Puhu.Btop.Platform.Windows;
using Xunit;

namespace Puhu.Btop.Tests.Platform;

[SupportedOSPlatform("windows")]
public class WindowsPlatformSmokeTests
{
    [Fact]
    public void DiskMetrics_after_initialize_returns_without_throwing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        using var metrics = new WindowsDiskMetrics();
        metrics.Initialize();
        Thread.Sleep(1100);
        var (read, write, active) = metrics.GetMetrics("C:");
        Assert.InRange(active, 0, 100);
    }

    [Fact]
    public void ProcessTree_builds_without_throwing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        var tree = new WindowsProcessTree().BuildTree(Environment.ProcessId);
        Assert.NotNull(tree);
    }

    [Fact]
    public void ReadParentMap_includes_this_process_with_a_real_parent()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");

        var map = new WindowsProcessTree().ReadParentMap();

        // The running test process must be present with a non-self parent — this is
        // the data ProcessMonitorActor needs to populate ParentPid for tree mode.
        Assert.True(map.TryGetValue(Environment.ProcessId, out var parent));
        Assert.NotEqual(Environment.ProcessId, parent);
    }
}
