// Calls are runtime-guarded by Assert.SkipUnless(OperatingSystem.IsWindows()).
#pragma warning disable CA1416 // Validate platform compatibility
using Puhu.Btop.Platform.Windows;
using Xunit;

namespace Puhu.Btop.Tests.Platform;

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
}
#pragma warning restore CA1416
