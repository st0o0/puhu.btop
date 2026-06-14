// Calls are runtime-guarded by Assert.SkipUnless(OperatingSystem.IsWindows()).
using System.Runtime.Versioning;
using Puhu.Btop.Platform.Windows;
using Xunit;

namespace Puhu.Btop.Tests.Platform;

[SupportedOSPlatform("windows")]
public class WindowsPlatformSmokeTests
{
    [Fact(Timeout = 30000)]
    public async Task DiskMetrics_after_initialize_returns_without_throwing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        using var metrics = new WindowsDiskMetrics();
        metrics.Initialize();
        // Let the PDH counter accumulate a sample before reading.
        await Task.Delay(1100, TestContext.Current.CancellationToken);
        var (read, write, active) = metrics.GetMetrics("C:");
        Assert.InRange(active, 0, 100);
    }

    [Fact(Timeout = 30000)]
    public void ProcessTree_builds_without_throwing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        var tree = new WindowsProcessTree().BuildTree(Environment.ProcessId);
        Assert.NotNull(tree);
    }

    [Fact(Timeout = 30000)]
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
