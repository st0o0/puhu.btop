using Akka.Actor;
using Akka.Hosting;
using Puhu.Btop.Actors;
using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Nodes;
using Puhu.Btop.Services;
using Puhu.Plugin;
using R3;
using Termina.Input;
using Termina.Layout;
using Termina.Notifications;
using Termina.Reactive;
using Termina.Terminal;

namespace Puhu.Btop.Pages;

public enum BtopSortField
{
    CpuPercent,
    RamPercent,
    Name,
    Pid
}

/// <summary>A process action awaiting y/n confirmation (terminate or kill).</summary>
public sealed record PendingProcessAction(int Pid, string ProcessName, string Verb);

public class BtopViewModel : ReactiveViewModel
{
    private readonly IMonitorDemand _demand;
    private readonly IGpuMetrics _gpuMetrics;
    private readonly ISettingsStore _settings;
    private readonly ITickSource _tickSource;
    private readonly IRequiredActor<MonitoringSupervisor> _supervisor;
    private readonly IToastService _toasts;
    private readonly List<IDisposable> _demandHandles = [];
    private IActorRef? _supervisorActor;
    private bool _disposed;

    public ReactiveProperty<double> CpuTotal { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<double>> CpuCores { get; } = new([]);
    public ReactiveProperty<string> CpuName { get; } = new("Loading...");
    public ReactiveProperty<ulong> RamTotal { get; } = new(0);
    public ReactiveProperty<ulong> RamUsed { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);
    public ReactiveProperty<GpuSnapshot?> Gpu { get; } = new(null);
    public bool GpuAvailable => _gpuMetrics.IsAvailable;

    public ReactiveProperty<IReadOnlyList<ProcessSnapshot>> AllProcesses { get; } = new([]);
    
    public ReactiveProperty<string> ProcessFilter { get; } = new("");
    public ReactiveProperty<bool> IsFilterMode { get; } = new(false);
    public ReactiveProperty<BtopSortField> SortField { get; }
    public ReactiveProperty<bool> SortDescending { get; }
    public ReactiveProperty<int> ActivePreset { get; }
    public ReactiveProperty<string> StatusHint { get; } = new("");

    // Tree (process hierarchy) presentation toggle — built client-side from the
    // flat process list + ParentPid, no actor round-trip required.
    public ReactiveProperty<bool> TreeMode { get; }

    // Pending terminate/kill action awaiting y/n confirmation. Null when none.
    public ReactiveProperty<PendingProcessAction?> PendingAction { get; } = new(null);

    // Panel visibility toggles (1–5 keys)
    public ReactiveProperty<bool> ShowCpu { get; }
    public ReactiveProperty<bool> ShowMemory { get; }
    public ReactiveProperty<bool> ShowNetDisk { get; }
    public ReactiveProperty<bool> ShowProcesses { get; }
    public ReactiveProperty<bool> ShowGpu { get; }

    public GraphStyle GraphStyleSetting { get; }

    // ListNode reference set by BtopPage so arrow keys can scroll it
    public IScrollableList? ProcessListNode { get; set; }

    // Set by BtopPage so terminate/kill can target the highlighted row.
    public Func<ProcessSnapshot?>? GetSelectedProcess { get; set; }

    public MetricStore Store { get; }

    private static readonly string[] PresetNames = ["Standard", "CPU Focus", "Resource Grid", "Minimal"];

    public BtopViewModel(
        MetricStore store,
        IMonitorDemand demand,
        IGpuMetrics gpuMetrics,
        ISettingsStore settings,
        ITickSource tickSource,
        IRequiredActor<MonitoringSupervisor> supervisor,
        IToastService toasts)
    {
        Store = store;
        _demand = demand;
        _gpuMetrics = gpuMetrics;
        _settings = settings;
        _tickSource = tickSource;
        _supervisor = supervisor;
        _toasts = toasts;

        TreeMode = new ReactiveProperty<bool>(settings.Get<bool?>("tree-mode") ?? false);

        ActivePreset = new ReactiveProperty<int>(
            Math.Clamp(settings.Get<int?>("preset") ?? 0, 0, 3));

        ShowCpu = new ReactiveProperty<bool>(settings.Get<bool?>("show-cpu") ?? true);
        ShowMemory = new ReactiveProperty<bool>(settings.Get<bool?>("show-memory") ?? true);
        ShowNetDisk = new ReactiveProperty<bool>(settings.Get<bool?>("show-netdisk") ?? true);
        ShowProcesses = new ReactiveProperty<bool>(settings.Get<bool?>("show-processes") ?? true);
        ShowGpu = new ReactiveProperty<bool>(settings.Get<bool?>("show-gpu") ?? true);

        SortField = new ReactiveProperty<BtopSortField>(
            settings.Get<int?>("sort-field") is { } sf && Enum.IsDefined((BtopSortField)sf)
                ? (BtopSortField)sf
                : BtopSortField.CpuPercent);
        SortDescending = new ReactiveProperty<bool>(settings.Get<bool?>("sort-descending") ?? true);

        GraphStyleSetting = settings.Get<string>("graph-style") switch
        {
            "block" => GraphStyle.Blocks,
            _ => GraphStyle.Braille,
        };
    }

    public override void OnActivated()
    {
        AcquireForVisibility(MetricKind.Disk, ShowNetDisk.Value);
        AcquireForVisibility(MetricKind.Network, ShowNetDisk.Value);
        AcquireForVisibility(MetricKind.Process, ShowProcesses.Value);
        if (_gpuMetrics.IsAvailable)
        {
            AcquireForVisibility(MetricKind.Gpu, ShowGpu.Value);
        }

        // Demand follows box visibility: release the underlying monitors when the
        // owning box is hidden, re-acquire when shown again.
        ShowNetDisk.Subscribe(visible =>
        {
            AcquireForVisibility(MetricKind.Disk, visible);
            AcquireForVisibility(MetricKind.Network, visible);
            PersistShow("show-netdisk", visible);
        }).DisposeWith(Subscriptions);

        ShowProcesses.Subscribe(visible =>
        {
            AcquireForVisibility(MetricKind.Process, visible);
            PersistShow("show-processes", visible);
        }).DisposeWith(Subscriptions);

        if (_gpuMetrics.IsAvailable)
        {
            ShowGpu.Subscribe(visible =>
            {
                AcquireForVisibility(MetricKind.Gpu, visible);
                PersistShow("show-gpu", visible);
            }).DisposeWith(Subscriptions);
        }

        ShowCpu.Subscribe(visible => PersistShow("show-cpu", visible)).DisposeWith(Subscriptions);
        ShowMemory.Subscribe(visible => PersistShow("show-memory", visible)).DisposeWith(Subscriptions);

        Store.Cpu.Subscribe(s =>
        {
            if (s is null)
            {
                return;
            }

            CpuName.Value = s.Name;
            CpuTotal.Value = s.TotalPercent;
            CpuCores.Value = s.CorePercents;
            if (!IsFilterMode.Value)
            {
                UpdateStatusHint();
            }
        }).DisposeWith(Subscriptions);

        Store.Memory.Subscribe(s =>
        {
            if (s is null)
            {
                return;
            }

            RamTotal.Value = s.TotalBytes;
            RamUsed.Value = s.UsedBytes;
            if (!IsFilterMode.Value)
            {
                UpdateStatusHint();
            }
        }).DisposeWith(Subscriptions);

        Store.Disks.Subscribe(d => { Disks.Value = d; }).DisposeWith(Subscriptions);

        Store.Networks.Subscribe(n => { Networks.Value = n; }).DisposeWith(Subscriptions);

        Store.Processes.Subscribe(p => { AllProcesses.Value = p; }).DisposeWith(Subscriptions);

        Store.Gpu.Subscribe(g =>
        {
            if (g is null)
            {
                return;
            }

            Gpu.Value = g;
        }).DisposeWith(Subscriptions);

        UpdateStatusHint();

        var resolve = _supervisor.GetAsync(CancellationToken.None);
        if (resolve.IsCompletedSuccessfully)
        {
            _supervisorActor = resolve.Result;
        }
        else
        {
            _ = ResolveSupervisorAsync(resolve);
        }

        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);
    }

    private async Task ResolveSupervisorAsync(Task<IActorRef> resolve)
    {
        try
        {
            _supervisorActor = await resolve;
        }
        catch
        {
            // Supervisor unavailable — terminate/kill will be no-ops until resolved.
        }
    }

    private void PersistShow(string key, bool value) => _settings.Set(key, value);

    private void UpdateStatusHint()
    {
        if (PendingAction.Value is { } pending)
        {
            StatusHint.Value =
                $" {pending.Verb} PID {pending.Pid} ({pending.ProcessName})?  [y] Confirm  [n/Esc] Cancel";
            return;
        }

        if (IsFilterMode.Value)
        {
            StatusHint.Value = ProcessFilter.Value.Length > 0
                ? $" Filter: \"{ProcessFilter.Value}█\"  [Enter] Apply  [Esc] Clear"
                : " Filter: █   (type to filter, [Enter] apply, [Esc] cancel)";
            return;
        }

        var cpu = $"CPU: {CpuTotal.Value:F1}%";
        var ramUsedGb = RamUsed.Value / 1024.0 / 1024 / 1024;
        var ramTotalGb = RamTotal.Value / 1024.0 / 1024 / 1024;
        var ram = $"RAM: {ramUsedGb:F1}/{ramTotalGb:F1} GiB";
        var layout = $"Layout: {PresetNames[ActivePreset.Value]}";
        var sort = $"[←→] Sort: {SortField.Value}";
        var dir = SortDescending.Value ? "↓" : "↑";
        var tree = TreeMode.Value ? "[e] Tree:on" : "[e] Tree:off";
        var rate = $"{_tickSource.CurrentInterval.TotalMilliseconds:F0}ms";
        StatusHint.Value =
            $" {cpu}  {ram}  |  {layout}  |  [F] Filter  [P] Preset  {sort}  [R] {dir}  {tree}  |  {rate}";
    }

    /// <summary>
    /// Column header for the process table. The active sort column is marked with a
    /// ▼ (descending) or ▲ (ascending) arrow so it's visible in the table itself.
    /// Field widths are kept in lock-step with the data-row format in <c>BtopPage</c>.
    /// </summary>
    public string BuildProcessHeader()
    {
        var arrow = SortDescending.Value ? '▼' : '▲';

        string Col(BtopSortField field, string label) =>
            SortField.Value == field ? $"{label}{arrow}" : label;

        return $" {Col(BtopSortField.Pid, "PID"),6}  {Col(BtopSortField.Name, "Name"),-22} " +
               $"{Col(BtopSortField.CpuPercent, "CPU%"),6}  {Col(BtopSortField.RamPercent, "RAM"),7}";
    }

    public IReadOnlyList<ProcessSnapshot> GetFilteredProcesses()
    {
        var source = AllProcesses.Value.AsEnumerable();

        if (!string.IsNullOrEmpty(ProcessFilter.Value))
        {
            source = source.Where(p =>
                p.Name.Contains(ProcessFilter.Value, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(ProcessFilter.Value));
        }

        source = SortField.Value switch
        {
            BtopSortField.CpuPercent => SortDescending.Value
                ? source.OrderByDescending(p => p.CpuPercent)
                : source.OrderBy(p => p.CpuPercent),
            BtopSortField.RamPercent => SortDescending.Value
                ? source.OrderByDescending(p => p.WorkingSetBytes)
                : source.OrderBy(p => p.WorkingSetBytes),
            BtopSortField.Name => SortDescending.Value
                ? source.OrderByDescending(p => p.Name)
                : source.OrderBy(p => p.Name),
            BtopSortField.Pid => SortDescending.Value
                ? source.OrderByDescending(p => p.Pid)
                : source.OrderBy(p => p.Pid),
            _ => source.OrderByDescending(p => p.CpuPercent)
        };

        var list = source.ToList();
        return TreeMode.Value ? OrderAsTree(list) : list;
    }

    // Maps pid → indentation depth for the most recent tree-ordered list.
    // Used by the page to indent process names when TreeMode is on.
    private readonly Dictionary<int, int> _treeDepth = new();

    public int GetTreeDepth(int pid) =>
        TreeMode.Value && _treeDepth.TryGetValue(pid, out var d) ? d : 0;

    /// <summary>
    /// Re-orders a flat process list into parent-then-children order using
    /// <see cref="ProcessSnapshot.ParentPid"/>, recording each row's depth in
    /// <see cref="_treeDepth"/>. Built entirely client-side — no actor query.
    /// Relative ordering among siblings (and roots) follows the incoming sort.
    /// </summary>
    private List<ProcessSnapshot> OrderAsTree(List<ProcessSnapshot> flat)
    {
        _treeDepth.Clear();
        var present = flat.Select(p => p.Pid).ToHashSet();
        var childrenByParent = flat
            .Where(p => p.ParentPid != p.Pid && present.Contains(p.ParentPid))
            .GroupBy(p => p.ParentPid)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ordered = new List<ProcessSnapshot>(flat.Count);
        var visited = new HashSet<int>();

        void Emit(ProcessSnapshot proc, int depth)
        {
            if (!visited.Add(proc.Pid))
            {
                return; // guard against cycles
            }

            _treeDepth[proc.Pid] = depth;
            ordered.Add(proc);
            if (childrenByParent.TryGetValue(proc.Pid, out var kids))
            {
                foreach (var kid in kids)
                {
                    Emit(kid, depth + 1);
                }
            }
        }

        // Roots: processes whose parent isn't in the visible set.
        foreach (var proc in flat.Where(p => p.ParentPid == p.Pid || !present.Contains(p.ParentPid)))
        {
            Emit(proc, 0);
        }

        // Safety net: emit any process not reached (e.g. orphaned by cycle guard).
        foreach (var proc in flat.Where(p => !visited.Contains(p.Pid)))
        {
            Emit(proc, 0);
        }

        return ordered;
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsFilterMode.Value)
        {
            HandleFilterKey(key);
            return;
        }

        // A terminate/kill confirmation is pending: only y/n/Esc matter.
        if (PendingAction.Value is { } pending)
        {
            HandleConfirmKey(key, pending);
            return;
        }

        switch (key.KeyInfo.Key)
        {
            // Tree presentation toggle (built client-side from ParentPid).
            case ConsoleKey.E:
                TreeMode.Value = !TreeMode.Value;
                _settings.Set("tree-mode", TreeMode.Value);
                UpdateStatusHint();
                break;

            // Cycle the active sort column with the arrow keys.
            case ConsoleKey.LeftArrow:
                ShiftSortField(-1);
                break;
            case ConsoleKey.RightArrow:
                ShiftSortField(+1);
                break;

            // Terminate / kill the selected process (confirm first).
            case ConsoleKey.T:
                RequestProcessAction("Terminate");
                break;
            case ConsoleKey.K:
                RequestProcessAction("Kill");
                break;

            case ConsoleKey.P:
                var next = (ActivePreset.Value + 1) % 4;
                ActivePreset.Value = next;
                _settings.Set("preset", next);
                UpdateStatusHint();
                break;

            // Box visibility toggles. In Puhu, cross-tab navigation uses Tab
            // (see RegisterGlobalKeys), so the digit keys are free for box toggles.
            // NumPad variants are kept for parity with btop.
            case ConsoleKey.D1 or ConsoleKey.NumPad1:
                if (CountVisible() > 1 || !ShowCpu.Value)
                {
                    ShowCpu.Value = !ShowCpu.Value;
                }

                break;
            case ConsoleKey.D2 or ConsoleKey.NumPad2:
                if (CountVisible() > 1 || !ShowMemory.Value)
                {
                    ShowMemory.Value = !ShowMemory.Value;
                }

                break;
            case ConsoleKey.D3 or ConsoleKey.NumPad3:
                if (CountVisible() > 1 || !ShowNetDisk.Value)
                {
                    ShowNetDisk.Value = !ShowNetDisk.Value;
                }

                break;
            case ConsoleKey.D4 or ConsoleKey.NumPad4:
                if (CountVisible() > 1 || !ShowProcesses.Value)
                {
                    ShowProcesses.Value = !ShowProcesses.Value;
                }

                break;
            case ConsoleKey.D5 or ConsoleKey.NumPad5:
                // GPU box only exists when a GPU was detected.
                if (_gpuMetrics.IsAvailable && (CountVisible() > 1 || !ShowGpu.Value))
                {
                    ShowGpu.Value = !ShowGpu.Value;
                }

                break;

            case ConsoleKey.UpArrow: ProcessListNode?.MoveUp(); break;
            case ConsoleKey.DownArrow: ProcessListNode?.MoveDown(); break;
            case ConsoleKey.PageUp: ProcessListNode?.PageUp(); break;
            case ConsoleKey.PageDown: ProcessListNode?.PageDown(); break;

            case ConsoleKey.M:
                CycleSortField();
                break;

            case ConsoleKey.R:
                SortDescending.Value = !SortDescending.Value;
                _settings.Set("sort-descending", SortDescending.Value);
                UpdateStatusHint();
                break;

            case ConsoleKey.F:
                IsFilterMode.Value = true;
                UpdateStatusHint();
                break;
        }
    }

    public void CycleSortField()
    {
        SortField.Value = SortField.Value switch
        {
            BtopSortField.CpuPercent => BtopSortField.RamPercent,
            BtopSortField.RamPercent => BtopSortField.Name,
            BtopSortField.Name => BtopSortField.Pid,
            _ => BtopSortField.CpuPercent,
        };
        _settings.Set("sort-field", (int)SortField.Value);
        UpdateStatusHint();
    }

    /// <summary>Cycle the active sort column left (-1) or right (+1), wrapping.</summary>
    public void ShiftSortField(int direction)
    {
        var count = Enum.GetValues<BtopSortField>().Length;
        var next = ((int)SortField.Value + direction + count) % count;
        SortField.Value = (BtopSortField)next;
        _settings.Set("sort-field", (int)SortField.Value);
        UpdateStatusHint();
    }

    /// <summary>
    /// Stage a terminate/kill confirmation for the selected process. On Windows
    /// there is no SIGTERM/SIGKILL distinction (<see cref="System.Diagnostics.Process.Kill()"/>
    /// is the only option), so both verbs dispatch the same <see cref="KillProcess"/>
    /// command; the verb only changes the confirmation prompt.
    /// </summary>
    public void RequestProcessAction(string verb)
    {
        if (GetSelectedProcess?.Invoke() is not { } proc)
        {
            // Without a toast this was completely silent — the user couldn't tell
            // whether the keypress did anything.
            _toasts.Show("No process selected", new ToastOptions(Color: Color.Yellow));
            return;
        }

        PendingAction.Value = new PendingProcessAction(proc.Pid, proc.Name, verb);
        UpdateStatusHint();
    }

    private void HandleConfirmKey(KeyPressed key, PendingProcessAction pending)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Y:
                DispatchProcessAction(pending);
                PendingAction.Value = null;
                UpdateStatusHint();
                break;
            case ConsoleKey.N:
            case ConsoleKey.Escape:
                PendingAction.Value = null;
                UpdateStatusHint();
                break;
        }
    }

    /// <summary>
    /// Dispatches the confirmed terminate/kill and surfaces the outcome as a toast.
    /// Uses <c>Ask</c> (not the old fire-and-forget <c>Tell(NoSender)</c>) so the
    /// real result — success, access-denied, already-exited — is reported instead of
    /// silently dropped.
    /// </summary>
    private void DispatchProcessAction(PendingProcessAction pending)
    {
        // Immediate acknowledgement that the confirm registered.
        _toasts.Show($"{pending.Verb}: PID {pending.Pid} ({pending.ProcessName}) …");

        if (_supervisorActor is not { } supervisor)
        {
            return;
        }

        Observable
            .FromAsync(ct => new ValueTask<object>(
                supervisor.Ask<object>(new KillProcess(pending.Pid), TimeSpan.FromSeconds(3), ct)))
            .Subscribe(
                result =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    switch (result)
                    {
                        case ActionSuccess s:
                            _toasts.Show(s.Message, new ToastOptions(Color: Color.Green));
                            break;
                        case ActionFailure f:
                            _toasts.Show($"Failed: {f.Error}", new ToastOptions(Color: Color.Red));
                            break;
                    }
                },
                static _ => { }, // onErrorResume: ignore Ask timeout/cancellation
                static _ => { }) // onCompleted
            .DisposeWith(Subscriptions);
    }

    private void HandleFilterKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Enter:
                // Apply the filter and leave edit mode. The filter stays active, so
                // the list can now be scrolled, sorted, and acted on (kill/term).
                IsFilterMode.Value = false;
                UpdateStatusHint();
                break;
            case ConsoleKey.Escape:
                IsFilterMode.Value = false;
                ProcessFilter.Value = "";
                UpdateStatusHint();
                break;
            case ConsoleKey.Backspace:
                if (ProcessFilter.Value.Length > 0)
                {
                    ProcessFilter.Value = ProcessFilter.Value[..^1];
                    UpdateStatusHint();
                }

                break;
            default:
                if (key.KeyInfo.KeyChar is >= ' ' and <= '~')
                {
                    ProcessFilter.Value += key.KeyInfo.KeyChar;
                    UpdateStatusHint();
                }

                break;
        }
    }

    private int CountVisible() =>
        (ShowCpu.Value ? 1 : 0) + (ShowMemory.Value ? 1 : 0) +
        (ShowNetDisk.Value ? 1 : 0) + (ShowProcesses.Value ? 1 : 0) +
        (_gpuMetrics.IsAvailable && ShowGpu.Value ? 1 : 0);

    private void AcquireForVisibility(MetricKind kind, bool visible)
    {
        // Track one handle per kind so we can release on hide and re-acquire on show.
        var existing = _demandHandles.OfType<KindHandle>().FirstOrDefault(h => h.Kind == kind);
        if (visible)
        {
            if (existing is not null)
            {
                return;
            }

            _demandHandles.Add(new KindHandle(kind, _demand.Acquire(kind)));
        }
        else
        {
            if (existing is null)
            {
                return;
            }

            existing.Dispose();
            _demandHandles.Remove(existing);
        }
    }

    private sealed class KindHandle(MetricKind kind, IDisposable inner) : IDisposable
    {
        public MetricKind Kind => kind;
        public void Dispose() => inner.Dispose();
    }

    public override void OnDeactivating()
    {
        foreach (var d in _demandHandles)
        {
            d.Dispose();
        }

        _demandHandles.Clear();
        base.OnDeactivating();
    }

    public override void Dispose()
    {
        _disposed = true;
        CpuTotal.Dispose();
        CpuCores.Dispose();
        CpuName.Dispose();
        RamTotal.Dispose();
        RamUsed.Dispose();
        Disks.Dispose();
        Networks.Dispose();
        Gpu.Dispose();
        AllProcesses.Dispose();
        ProcessFilter.Dispose();
        IsFilterMode.Dispose();
        SortField.Dispose();
        SortDescending.Dispose();
        ActivePreset.Dispose();
        StatusHint.Dispose();
        ShowCpu.Dispose();
        ShowMemory.Dispose();
        ShowNetDisk.Dispose();
        ShowProcesses.Dispose();
        ShowGpu.Dispose();
        TreeMode.Dispose();
        PendingAction.Dispose();
        base.Dispose();
    }
}