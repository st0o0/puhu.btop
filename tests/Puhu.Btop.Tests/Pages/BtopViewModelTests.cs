using System.Reflection;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Pages;
using Puhu.Btop.Services;
using Puhu.Plugin;
using R3;
using Termina.Input;
using Termina.Reactive;

namespace Puhu.Btop.Tests.Pages;

public class BtopViewModelTests
{
    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public readonly Dictionary<string, object?> Values = new();

        public T? Get<T>(string key) =>
            Values.TryGetValue(key, out var v) && v is T t ? t : default;

        public void Set<T>(string key, T value) => Values[key] = value;

        public Observable<T> Observe<T>(string key) => Observable.Empty<T>();

        public void Remove(string key) => Values.Remove(key);
    }

    private sealed class CountingDemand : IMonitorDemand
    {
        public readonly Dictionary<MetricKind, int> Acquired = new();
        public readonly Dictionary<MetricKind, int> Released = new();

        public int Live(MetricKind kind) =>
            (Acquired.GetValueOrDefault(kind)) - (Released.GetValueOrDefault(kind));

        public IDisposable Acquire(MetricKind kind)
        {
            Acquired[kind] = Acquired.GetValueOrDefault(kind) + 1;
            return new Releaser(() => Released[kind] = Released.GetValueOrDefault(kind) + 1);
        }

        private sealed class Releaser(Action onDispose) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) onDispose();
            }
        }
    }

    private sealed class FakeTickSource : ITickSource
    {
        public TimeSpan CurrentInterval => TimeSpan.FromMilliseconds(500);
        public Observable<Tick> Ticks => Observable.Empty<Tick>();
        public IDisposable Subscribe(Action onTick) => Disposable.Empty;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static BtopViewModel CreateVm(
        out FakeSettingsStore settings,
        out CountingDemand demand,
        MetricStore? store = null,
        IGpuMetrics? gpu = null)
    {
        settings = new FakeSettingsStore();
        demand = new CountingDemand();
        var vm = new BtopViewModel(
            store ?? new MetricStore(),
            demand,
            gpu ?? NoGpuMetrics.Instance,
            settings,
            new FakeTickSource());

        // The framework wires Input via an internal WireUp call when binding to a
        // page. Tests don't go through a page, so wire an empty input stream by
        // reflection so OnActivated's input subscription has something to attach to.
        var wireUp = typeof(ReactiveViewModel).GetMethod(
            "WireUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        wireUp.Invoke(vm, new object?[]
        {
            (Action<string>)(_ => { }),
            (Action<string, object?>)((_, _) => { }),
            (Action)(() => { }),
            (Action)(() => { }),
            Observable.Empty<IInputEvent>(),
        });

        return vm;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void TogglingBoxOffThenOn_ReleasesThenReAcquiresDemand()
    {
        var vm = CreateVm(out _, out var demand);
        vm.OnActivated();

        // NetDisk box acquires both Disk and Network demand on activation.
        Assert.Equal(1, demand.Live(MetricKind.Disk));
        Assert.Equal(1, demand.Live(MetricKind.Network));

        // Toggle the NetDisk box off → demand released.
        vm.ShowNetDisk.Value = false;
        Assert.Equal(0, demand.Live(MetricKind.Disk));
        Assert.Equal(0, demand.Live(MetricKind.Network));
        Assert.Equal(1, demand.Released.GetValueOrDefault(MetricKind.Disk));

        // Toggle back on → demand re-acquired.
        vm.ShowNetDisk.Value = true;
        Assert.Equal(1, demand.Live(MetricKind.Disk));
        Assert.Equal(1, demand.Live(MetricKind.Network));
        Assert.Equal(2, demand.Acquired.GetValueOrDefault(MetricKind.Disk));

        vm.Dispose();
    }

    [Fact]
    public void SettingProcessFilter_NarrowsFilteredProcessList()
    {
        var vm = CreateVm(out _, out _);

        vm.AllProcesses.Value = new List<ProcessSnapshot>
        {
            Proc(1, "chrome"),
            Proc(2, "dotnet"),
            Proc(3, "chromedriver"),
            Proc(4, "explorer"),
        };

        // No filter → all four visible.
        Assert.Equal(4, vm.GetFilteredProcesses().Count);

        vm.ProcessFilter.Value = "chrome";
        var filtered = vm.GetFilteredProcesses();

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, p => Assert.Contains("chrome", p.Name, StringComparison.OrdinalIgnoreCase));

        vm.Dispose();
    }

    [Fact]
    public void CyclingSortField_PersistsToSettingsStore()
    {
        var vm = CreateVm(out var settings, out _);

        Assert.Equal(BtopSortField.CpuPercent, vm.SortField.Value);

        vm.CycleSortField();
        Assert.Equal(BtopSortField.RamPercent, vm.SortField.Value);
        Assert.True(settings.Values.ContainsKey("sort-field"));
        Assert.Equal((int)BtopSortField.RamPercent, settings.Get<int?>("sort-field"));

        vm.CycleSortField();
        Assert.Equal(BtopSortField.Name, vm.SortField.Value);
        Assert.Equal((int)BtopSortField.Name, settings.Get<int?>("sort-field"));

        vm.CycleSortField();
        Assert.Equal(BtopSortField.Pid, vm.SortField.Value);

        // Cycle wraps back to CpuPercent.
        vm.CycleSortField();
        Assert.Equal(BtopSortField.CpuPercent, vm.SortField.Value);
        Assert.Equal((int)BtopSortField.CpuPercent, settings.Get<int?>("sort-field"));

        vm.Dispose();
    }

    private static ProcessSnapshot Proc(int pid, string name) =>
        new(pid, name, ProcessGroup.Apps, 1.0, 1024L * 1024, 0, 0, 1, 1, "user", 0);
}
