using System.Reflection;
using Akka.Actor;
using Akka.Hosting;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Pages;
using Puhu.Btop.Services;
using Puhu.Plugin;
using R3;
using Termina.Input;
using Termina.Notifications;
using Termina.Reactive;

namespace Puhu.Btop.Tests.Pages;

public class BtopViewModelTests : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("btop-vm-tests");
    private RecordingToastService _toasts = new();

    public void Dispose() => _system.Dispose();

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

    /// <summary>Records every toast so tests can assert user feedback was shown.</summary>
    private sealed class RecordingToastService : IToastService
    {
        public readonly List<string> Messages = new();
        public Observable<ToastMessage?> CurrentToast => Observable.Empty<ToastMessage?>();
        public void Show(string message, ToastOptions? options = null) => Messages.Add(message);
    }

    private sealed class FakeGpuMetrics : IGpuMetrics
    {
        public bool IsAvailable => true;
        public GpuSnapshot GetSnapshot() => new("Fake GPU", 0, 0, 0, 0);
    }

    /// <summary>
    /// IRequiredActor whose ref is an <see cref="Inbox"/> receiver, so messages the
    /// VM tells the supervisor land in a queue the test can assert on — a real
    /// behavioural probe, not a self-asserting mock.
    /// </summary>
    private sealed class ProbeRequiredActor(IActorRef probe) : IRequiredActor<MonitoringSupervisor>
    {
        public IActorRef ActorRef => probe;

        public Task<IActorRef> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(probe);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static KeyPressed Key(ConsoleKey key, char ch = '\0') =>
        new(new ConsoleKeyInfo(ch, key, false, false, false));

    private BtopViewModel CreateVm(
        out FakeSettingsStore settings,
        out CountingDemand demand,
        out Subject<IInputEvent> input,
        out Inbox supervisorInbox,
        MetricStore? store = null,
        IGpuMetrics? gpu = null)
    {
        settings = new FakeSettingsStore();
        demand = new CountingDemand();
        input = new Subject<IInputEvent>();
        supervisorInbox = Inbox.Create(_system);
        _toasts = new RecordingToastService();
        var vm = new BtopViewModel(
            store ?? new MetricStore(),
            demand,
            gpu ?? NoGpuMetrics.Instance,
            settings,
            new FakeTickSource(),
            new ProbeRequiredActor(supervisorInbox.Receiver),
            _toasts);

        // The framework wires Input via an internal WireUp call when binding to a
        // page. Tests don't go through a page, so wire an empty input stream by
        // reflection so OnActivated's input subscription has something to attach to.
        var wireUp = typeof(ReactiveViewModel).GetMethod(
            "WireUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        wireUp.Invoke(vm, [
            (Action<string>)(_ => { }),
            (Action<string, object?>)((_, _) => { }),
            (Action)(() => { }),
            (Action)(() => { }),
            input.AsObservable()
        ]);

        return vm;
    }

    private BtopViewModel CreateVm(out FakeSettingsStore settings, out CountingDemand demand) =>
        CreateVm(out settings, out demand, out _, out _);

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
    public void ToggleTreeMode_FlipsAndPersists()
    {
        var vm = CreateVm(out var settings, out _, out var input, out _);
        vm.OnActivated();

        Assert.False(vm.TreeMode.Value);

        // 'e' toggles tree mode on.
        input.OnNext(Key(ConsoleKey.E));
        Assert.True(vm.TreeMode.Value);
        Assert.True((bool)settings.Values["tree-mode"]!);

        // 'e' again toggles it off.
        input.OnNext(Key(ConsoleKey.E));
        Assert.False(vm.TreeMode.Value);
        Assert.False((bool)settings.Values["tree-mode"]!);

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void TreeMode_OrdersChildrenUnderParentsWithDepth()
    {
        var vm = CreateVm(out _, out _, out _, out _);
        vm.OnActivated();

        // 1 ── 2 ── 3   and a standalone root 4
        vm.AllProcesses.Value = new List<ProcessSnapshot>
        {
            Proc(3, "grandchild", ppid: 2),
            Proc(1, "root", ppid: 1),
            Proc(2, "child", ppid: 1),
            Proc(4, "other", ppid: 4),
        };
        vm.TreeMode.Value = true;

        var ordered = vm.GetFilteredProcesses();
        var pids = ordered.Select(p => p.Pid).ToList();

        // Parent precedes its descendants.
        Assert.True(pids.IndexOf(1) < pids.IndexOf(2));
        Assert.True(pids.IndexOf(2) < pids.IndexOf(3));
        Assert.Equal(0, vm.GetTreeDepth(1));
        Assert.Equal(1, vm.GetTreeDepth(2));
        Assert.Equal(2, vm.GetTreeDepth(3));
        Assert.Equal(0, vm.GetTreeDepth(4));

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void ArrowKeys_CycleSortFieldThroughAllColumnsAndPersist()
    {
        var vm = CreateVm(out var settings, out _, out var input, out _);
        vm.OnActivated();

        Assert.Equal(BtopSortField.CpuPercent, vm.SortField.Value);

        // Right cycles forward through every column and wraps.
        input.OnNext(Key(ConsoleKey.RightArrow));
        Assert.Equal(BtopSortField.RamPercent, vm.SortField.Value);
        Assert.Equal((int)BtopSortField.RamPercent, settings.Get<int?>("sort-field"));

        input.OnNext(Key(ConsoleKey.RightArrow));
        Assert.Equal(BtopSortField.Name, vm.SortField.Value);
        input.OnNext(Key(ConsoleKey.RightArrow));
        Assert.Equal(BtopSortField.Pid, vm.SortField.Value);
        input.OnNext(Key(ConsoleKey.RightArrow));
        Assert.Equal(BtopSortField.CpuPercent, vm.SortField.Value);

        // Left cycles backward, wrapping to the last column.
        input.OnNext(Key(ConsoleKey.LeftArrow));
        Assert.Equal(BtopSortField.Pid, vm.SortField.Value);
        Assert.Equal((int)BtopSortField.Pid, settings.Get<int?>("sort-field"));

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void KillConfirm_ConfirmDispatchesKill_CancelSendsNothing()
    {
        var vm = CreateVm(out _, out _, out var input, out var supervisor);
        var selected = Proc(4321, "victim");
        vm.GetSelectedProcess = () => selected;
        vm.OnActivated();

        // 'k' stages a pending confirmation — no command sent yet.
        input.OnNext(Key(ConsoleKey.K));
        Assert.NotNull(vm.PendingAction.Value);
        Assert.Equal(4321, vm.PendingAction.Value!.Pid);

        // 'n' cancels — pending cleared, nothing dispatched.
        input.OnNext(Key(ConsoleKey.N));
        Assert.Null(vm.PendingAction.Value);

        // 'k' then 'y' confirms — a KillProcess for the selected pid is dispatched.
        input.OnNext(Key(ConsoleKey.K));
        input.OnNext(Key(ConsoleKey.Y));
        Assert.Null(vm.PendingAction.Value);

        // The supervisor probe receives exactly the kill — and only the kill
        // (the earlier cancel sent nothing, so this is the first/only message).
        var msg = supervisor.Receive(TimeSpan.FromSeconds(3));
        var kill = Assert.IsType<KillProcess>(msg);
        Assert.Equal(4321, kill.Pid);
        Assert.Throws<TimeoutException>(() => supervisor.Receive(TimeSpan.FromMilliseconds(200)));

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void Enter_AppliesFilterAndLeavesEditMode()
    {
        var vm = CreateVm(out _, out _, out var input, out _);
        vm.OnActivated();

        // 'f' enters filter edit mode.
        input.OnNext(Key(ConsoleKey.F));
        Assert.True(vm.IsFilterMode.Value);

        // Type "ch".
        input.OnNext(Key(ConsoleKey.C, 'c'));
        input.OnNext(Key(ConsoleKey.H, 'h'));
        Assert.Equal("ch", vm.ProcessFilter.Value);

        // Enter applies: edit mode ends but the filter text is KEPT (the old code had
        // no Enter handler, so the only exit was Esc — which cleared the filter).
        input.OnNext(Key(ConsoleKey.Enter));
        Assert.False(vm.IsFilterMode.Value);
        Assert.Equal("ch", vm.ProcessFilter.Value);

        // And keys now act on the list again instead of being eaten as filter text.
        input.OnNext(Key(ConsoleKey.E));
        Assert.True(vm.TreeMode.Value);

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void Pressing5_TogglesGpuBox_WhenGpuAvailable()
    {
        var vm = CreateVm(out var settings, out _, out var input, out _, gpu: new FakeGpuMetrics());
        vm.OnActivated();

        Assert.True(vm.ShowGpu.Value);

        // '5' hides the GPU box…
        input.OnNext(Key(ConsoleKey.D5));
        Assert.False(vm.ShowGpu.Value);
        Assert.False((bool)settings.Values["show-gpu"]!);

        // …and shows it again.
        input.OnNext(Key(ConsoleKey.D5));
        Assert.True(vm.ShowGpu.Value);

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void Pressing5_DoesNothing_WhenNoGpu()
    {
        // NoGpuMetrics (default) → the GPU box doesn't exist, so '5' is inert.
        var vm = CreateVm(out _, out _, out var input, out _);
        vm.OnActivated();

        input.OnNext(Key(ConsoleKey.D5));
        Assert.True(vm.ShowGpu.Value); // unchanged default

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void ProcessHeader_MarksActiveSortColumnWithDirectionArrow()
    {
        var vm = CreateVm(out _, out _);

        // Default: CPU%, descending.
        var header = vm.BuildProcessHeader();
        Assert.Contains("CPU%▼", header);
        Assert.DoesNotContain("PID▼", header);

        // Switch to RAM, ascending.
        vm.ShiftSortField(+1); // CpuPercent → RamPercent
        vm.SortDescending.Value = false;
        var header2 = vm.BuildProcessHeader();
        Assert.Contains("RAM▲", header2);
        Assert.DoesNotContain("CPU%▼", header2);

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void KillWithNoSelection_ShowsToastInsteadOfSilentlyDoingNothing()
    {
        var vm = CreateVm(out _, out _, out var input, out _);
        vm.GetSelectedProcess = () => null;
        vm.OnActivated();

        input.OnNext(Key(ConsoleKey.K));

        Assert.Null(vm.PendingAction.Value);
        Assert.Contains(_toasts.Messages, m => m.Contains("No process"));

        vm.Dispose();
    }

    [Fact(Timeout = 30000)]
    public void ConfirmingKill_ShowsImmediateFeedbackToast()
    {
        var vm = CreateVm(out _, out _, out var input, out _);
        vm.GetSelectedProcess = () => Proc(4321, "victim");
        vm.OnActivated();

        input.OnNext(Key(ConsoleKey.K));
        input.OnNext(Key(ConsoleKey.Y));

        Assert.Contains(_toasts.Messages, m => m.Contains("4321") && m.Contains("victim"));

        vm.Dispose();
    }

    private static ProcessSnapshot Proc(int pid, string name, int ppid = 0) =>
        new(pid, name, ProcessGroup.Apps, 1.0, 1024L * 1024, 0, 0, 1, 1, "user", ppid);
}