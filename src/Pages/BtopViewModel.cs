using Puhu.Btop.Core.Messages;
using Puhu.Btop.Core.Models;
using Puhu.Btop.Core.Platform;
using Puhu.Btop.Nodes;
using Puhu.Btop.Services;
using Puhu.Plugin;
using R3;
using Termina.Input;
using Termina.Layout;
using Termina.Reactive;

namespace Puhu.Btop.Pages;

public enum BtopSortField { CpuPercent, RamPercent, Name, Pid }

public class BtopViewModel : ReactiveViewModel
{
    private readonly MetricStore _store;
    private readonly IMonitorDemand _demand;
    private readonly IGpuMetrics _gpuMetrics;
    private readonly ISettingsStore _settings;
    private readonly ITickSource _tickSource;
    private readonly List<IDisposable> _demandHandles = [];

    // ── Metrics ─────────────────────────────────────────────────────────────
    public ReactiveProperty<double> CpuTotal { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<double>> CpuCores { get; } = new([]);
    public ReactiveProperty<string> CpuName { get; } = new("Loading...");
    public ReactiveProperty<ulong> RamTotal { get; } = new(0);
    public ReactiveProperty<ulong> RamUsed { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);
    public ReactiveProperty<GpuSnapshot?> Gpu { get; } = new(null);
    public bool GpuAvailable => _gpuMetrics.IsAvailable;

    // ── Processes ────────────────────────────────────────────────────────────
    public ReactiveProperty<IReadOnlyList<ProcessSnapshot>> AllProcesses { get; } = new([]);

    // ── UI state ─────────────────────────────────────────────────────────────
    public ReactiveProperty<string> ProcessFilter { get; } = new("");
    public ReactiveProperty<bool> IsFilterMode { get; } = new(false);
    public ReactiveProperty<BtopSortField> SortField { get; }
    public ReactiveProperty<bool> SortDescending { get; }
    public ReactiveProperty<int> ActivePreset { get; }
    public ReactiveProperty<string> StatusHint { get; } = new("");

    // Panel visibility toggles (1–4 keys)
    public ReactiveProperty<bool> ShowCpu { get; }
    public ReactiveProperty<bool> ShowMemory { get; }
    public ReactiveProperty<bool> ShowNetDisk { get; }
    public ReactiveProperty<bool> ShowProcesses { get; }

    public GraphStyle GraphStyleSetting { get; }

    // ListNode reference set by BtopPage so arrow keys can scroll it
    public IScrollableList? ProcessListNode { get; set; }

    public MetricStore Store => _store;

    private static readonly string[] PresetNames = ["Standard", "CPU Focus", "Resource Grid", "Minimal"];

    public BtopViewModel(
        MetricStore store,
        IMonitorDemand demand,
        IGpuMetrics gpuMetrics,
        ISettingsStore settings,
        ITickSource tickSource)
    {
        _store = store;
        _demand = demand;
        _gpuMetrics = gpuMetrics;
        _settings = settings;
        _tickSource = tickSource;

        ActivePreset = new ReactiveProperty<int>(
            Math.Clamp(settings.Get<int?>("preset") ?? 0, 0, 3));

        ShowCpu = new ReactiveProperty<bool>(settings.Get<bool?>("show-cpu") ?? true);
        ShowMemory = new ReactiveProperty<bool>(settings.Get<bool?>("show-memory") ?? true);
        ShowNetDisk = new ReactiveProperty<bool>(settings.Get<bool?>("show-netdisk") ?? true);
        ShowProcesses = new ReactiveProperty<bool>(settings.Get<bool?>("show-processes") ?? true);

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
            AcquireForVisibility(MetricKind.Gpu, ShowCpu.Value);

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
            ShowCpu.Subscribe(visible =>
            {
                AcquireForVisibility(MetricKind.Gpu, visible);
            }).DisposeWith(Subscriptions);
        }

        ShowCpu.Subscribe(visible => PersistShow("show-cpu", visible)).DisposeWith(Subscriptions);
        ShowMemory.Subscribe(visible => PersistShow("show-memory", visible)).DisposeWith(Subscriptions);

        _store.Cpu.Subscribe(s =>
        {
            if (s is null) return;
            CpuName.Value = s.Name;
            CpuTotal.Value = s.TotalPercent;
            CpuCores.Value = s.CorePercents;
            if (!IsFilterMode.Value) UpdateStatusHint();
        }).DisposeWith(Subscriptions);

        _store.Memory.Subscribe(s =>
        {
            if (s is null) return;
            RamTotal.Value = s.TotalBytes;
            RamUsed.Value = s.UsedBytes;
            if (!IsFilterMode.Value) UpdateStatusHint();
        }).DisposeWith(Subscriptions);

        _store.Disks.Subscribe(d =>
        {
            Disks.Value = d;
        }).DisposeWith(Subscriptions);

        _store.Networks.Subscribe(n =>
        {
            Networks.Value = n;
        }).DisposeWith(Subscriptions);

        _store.Processes.Subscribe(p =>
        {
            AllProcesses.Value = p;
        }).DisposeWith(Subscriptions);

        _store.Gpu.Subscribe(g =>
        {
            if (g is null) return;
            Gpu.Value = g;
        }).DisposeWith(Subscriptions);

        UpdateStatusHint();

        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);
    }

    private void PersistShow(string key, bool value) => _settings.Set(key, value);

    private void UpdateStatusHint()
    {
        if (IsFilterMode.Value)
        {
            StatusHint.Value = ProcessFilter.Value.Length > 0
                ? $" Filter: \"{ProcessFilter.Value}\"  [Esc] Clear"
                : " Filter: █   (type to filter processes, Esc to cancel)";
            return;
        }

        var cpu = $"CPU: {CpuTotal.Value:F1}%";
        var ramUsedGb = RamUsed.Value / 1024.0 / 1024 / 1024;
        var ramTotalGb = RamTotal.Value / 1024.0 / 1024 / 1024;
        var ram = $"RAM: {ramUsedGb:F1}/{ramTotalGb:F1} GiB";
        var layout = $"Layout: {PresetNames[ActivePreset.Value]}";
        var sort = $"[M] Sort: {SortField.Value}";
        var dir = SortDescending.Value ? "↓" : "↑";
        var rate = $"{_tickSource.CurrentInterval.TotalMilliseconds:F0}ms";
        StatusHint.Value =
            $" {cpu}  {ram}  |  {layout}  |  [F] Filter  [P] Preset  {sort}  [R] {dir}  |  {rate}";
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

        return source.ToList();
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsFilterMode.Value)
        {
            HandleFilterKey(key);
            return;
        }

        switch (key.KeyInfo.Key)
        {
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
                if (CountVisible() > 1 || !ShowCpu.Value) ShowCpu.Value = !ShowCpu.Value;
                break;
            case ConsoleKey.D2 or ConsoleKey.NumPad2:
                if (CountVisible() > 1 || !ShowMemory.Value) ShowMemory.Value = !ShowMemory.Value;
                break;
            case ConsoleKey.D3 or ConsoleKey.NumPad3:
                if (CountVisible() > 1 || !ShowNetDisk.Value) ShowNetDisk.Value = !ShowNetDisk.Value;
                break;
            case ConsoleKey.D4 or ConsoleKey.NumPad4:
                if (CountVisible() > 1 || !ShowProcesses.Value) ShowProcesses.Value = !ShowProcesses.Value;
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

    private void HandleFilterKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
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
        (ShowNetDisk.Value ? 1 : 0) + (ShowProcesses.Value ? 1 : 0);

    private void AcquireForVisibility(MetricKind kind, bool visible)
    {
        // Track one handle per kind so we can release on hide and re-acquire on show.
        var existing = _demandHandles.OfType<KindHandle>().FirstOrDefault(h => h.Kind == kind);
        if (visible)
        {
            if (existing is not null) return;
            _demandHandles.Add(new KindHandle(kind, _demand.Acquire(kind)));
        }
        else
        {
            if (existing is null) return;
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
        foreach (var d in _demandHandles) d.Dispose();
        _demandHandles.Clear();
        base.OnDeactivating();
    }

    public override void Dispose()
    {
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
        base.Dispose();
    }
}
