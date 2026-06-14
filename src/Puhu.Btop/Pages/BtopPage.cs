using Puhu.Btop.Core.Models;
using Puhu.Btop.Nodes;
using Puhu.Plugin;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace Puhu.Btop.Pages;

public sealed class BtopPage : ReactivePage<BtopViewModel>, IKeyHintProvider
{
    private readonly ITabNavigator _tabNavigator;
    private readonly IThemeService _theme;

    private GraphNode? _cpuGraph;
    private GraphNode? _gpuGraph;
    private GraphNode? _ramGraph;
    private CoreMeterNode? _coresNode;
    private BtopBoxNode? _cpuBox;
    private DataListNode<ProcessSnapshot>? _processList;

    public BtopPage(ITabNavigator tabNavigator, IThemeService theme)
    {
        _tabNavigator = tabNavigator;
        _theme = theme;
    }

    public string[] GetKeyHints() =>
    [
        ViewModel.GpuAvailable ? "1-5:Boxes" : "1-4:Boxes",
        "↑↓:Select", "f:Filter", "e:Tree", "←→:Sort", "t:Term", "k:Kill",
    ];

    public override ILayoutNode BuildLayout()
    {
        var theme = _theme.Current;
        var graphStyle = ViewModel.GraphStyleSetting;

        _cpuGraph = new GraphNode(intervalMs: 0)
            .WithStyle(graphStyle)
            .WithGradient(theme.GraphGradient)
            .WithRange(0, 100);

        _ramGraph = new GraphNode(intervalMs: 0)
            .WithStyle(graphStyle)
            .WithGradient(theme.GraphGradient)
            .WithRange(0, 100);

        _gpuGraph = new GraphNode(intervalMs: 0)
            .WithStyle(graphStyle)
            .WithGradient(theme.GraphGradient)
            .WithRange(0, 100);

        _coresNode = new CoreMeterNode().WithGradient(BtopGradients.Resource(theme));

        _processList = new DataListNode<ProcessSnapshot>(
            p =>
            {
                var ramMb = p.WorkingSetBytes / 1024 / 1024;
                var depth = ViewModel.GetTreeDepth(p.Pid);
                var indent = depth > 0 ? new string(' ', depth * 2) : "";
                var budget = Math.Max(4, 22 - indent.Length);
                var rawName = p.Name.Length > budget ? p.Name[..(budget - 1)] + "…" : p.Name;
                var name = $"{indent}{rawName}";
                var ramStr = ramMb >= 1024 ? $"{ramMb / 1024.0,4:F1}GB" : $"{ramMb,4}MB";
                return $" {p.Pid,6}  {name,-22} {p.CpuPercent,5:F1}%  {ramStr,7}";
            },
            p => p.CpuPercent switch
            {
                > 80 => _theme.Current.Error,
                > 50 => _theme.Current.Warning,
                _ => _theme.Current.Foreground,
            });
        _processList.WithHighlightColors(_theme.Current.SelectionText, _theme.Current.Selection);

        ViewModel.ProcessListNode = _processList;
        ViewModel.GetSelectedProcess = () => _processList.SelectedItem;

        return Layouts.Vertical()
            .WithChild(ViewModel.ActivePreset
                .CombineLatest(ViewModel.ShowCpu, ViewModel.ShowMemory, ViewModel.ShowNetDisk,
                    ViewModel.ShowProcesses, ViewModel.ShowGpu,
                    (preset, _, _, _, _, _) => BuildGridForPreset(preset))
                .AsLayout().Fill())
            .WithChild(ViewModel.StatusHint
                .Select<string, ILayoutNode>(hint =>
                    new TextNode(hint).WithForeground(_theme.Current.StatusBarText))
                .AsLayout().Height(1));
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();

        KeyBindings.RegisterGlobalKeys(
            () => ViewModel.RequestShutdown(),
            path => Navigate(path),
            _tabNavigator);

        // Live theme re-render: theme colors/gradient are baked into nodes at
        // BuildLayout time, so a theme change requires rebuilding the layout tree.
        _theme.Changes
            .Subscribe(_ => InvalidateLayout())
            .DisposeWith(Subscriptions);

        ViewModel.Store.Cpu.Subscribe(s =>
        {
            if (s is null)
            {
                return;
            }

            _cpuGraph?.SetData(ViewModel.Store.CpuHistory.Snapshot());
            _coresNode?.SetCores(s.CorePercents);
        }).DisposeWith(Subscriptions);

        ViewModel.Store.Memory.Subscribe(s =>
        {
            if (s is null)
            {
                return;
            }

            _ramGraph?.SetData(ViewModel.Store.MemHistory.Snapshot());
        }).DisposeWith(Subscriptions);

        ViewModel.Store.Gpu.Subscribe(g =>
        {
            if (g is null)
            {
                return;
            }

            _gpuGraph?.SetData(ViewModel.Store.GpuHistory.Snapshot());
        }).DisposeWith(Subscriptions);

        Observable.Merge(
                ViewModel.AllProcesses.Select(_ => Unit.Default),
                ViewModel.ProcessFilter.Select(_ => Unit.Default),
                ViewModel.SortField.Select(_ => Unit.Default),
                ViewModel.SortDescending.Select(_ => Unit.Default),
                ViewModel.TreeMode.Select(_ => Unit.Default))
            .Subscribe(_ => _processList?.SetItems(ViewModel.GetFilteredProcesses()))
            .DisposeWith(Subscriptions);

        // Clock in the CPU box top border, updated once per second.
        Observable.Interval(TimeSpan.FromSeconds(1), TimeProvider.System)
            .Subscribe(_ =>
                _cpuBox?.WithCenterTitle(TimeProvider.System.GetLocalNow().ToString("HH:mm:ss")))
            .DisposeWith(Subscriptions);
    }

    // ── Preset layouts ───────────────────────────────────────────────────────

    private ILayoutNode BuildGridForPreset(int preset) => preset switch
    {
        1 => BuildPreset1CpuFocus(),
        2 => BuildPreset2ResourceGrid(),
        3 => BuildPreset3Minimal(),
        _ => BuildPreset0Standard(),
    };

    /// <summary>Preset 0 – Standard: top CPU[+GPU], middle MEM|NET/DISK, bottom Processes</summary>
    private ILayoutNode BuildPreset0Standard()
    {
        var showCpu = ViewModel.ShowCpu.Value;
        var showMem = ViewModel.ShowMemory.Value;
        var showNet = ViewModel.ShowNetDisk.Value;
        var showProc = ViewModel.ShowProcesses.Value;
        var showGpu = ViewModel.GpuAvailable && ViewModel.ShowGpu.Value;

        if (showGpu)
        {
            var hasMiddleRow = showMem || showNet;

            GridNode grid;
            if (hasMiddleRow)
            {
                grid = new GridNode(3, 2)
                    .WithColumnWidths(new SizeConstraint.Percent(60), new SizeConstraint.Fill())
                    .WithRowHeights(
                        new SizeConstraint.Percent(30),
                        new SizeConstraint.Percent(20),
                        new SizeConstraint.Fill());

                if (showCpu)
                {
                    grid.SetCell(0, 0, BuildCpuPanel());
                }

                grid.SetCell(0, showCpu ? 1 : 0, BuildGpuPanel(), colSpan: showCpu ? 1 : 2);

                switch (showMem)
                {
                    case true when showNet:
                        grid.SetCell(1, 0, BuildMemoryPanel());
                        grid.SetCell(1, 1, BuildNetDiskPanel());
                        break;
                    case true:
                        grid.SetCell(1, 0, BuildMemoryPanel(), colSpan: 2);
                        break;
                    default:
                        {
                            if (showNet)
                            {
                                grid.SetCell(1, 0, BuildNetDiskPanel(), colSpan: 2);
                            }

                            break;
                        }
                }

                if (showProc)
                {
                    grid.SetCell(2, 0, BuildProcessPanel(), colSpan: 2);
                }
            }
            else
            {
                grid = new GridNode(2, 2)
                    .WithColumnWidths(new SizeConstraint.Percent(60), new SizeConstraint.Fill())
                    .WithRowHeights(
                        new SizeConstraint.Percent(30),
                        new SizeConstraint.Fill());

                if (showCpu)
                {
                    grid.SetCell(0, 0, BuildCpuPanel());
                }

                grid.SetCell(0, showCpu ? 1 : 0, BuildGpuPanel(), colSpan: showCpu ? 1 : 2);

                if (showProc)
                {
                    grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 2);
                }
            }

            return grid;
        }
        else
        {
            // No GPU: 2-column grid, CPU spans both columns
            var hasMiddleRow = showMem || showNet;

            GridNode grid;
            if (hasMiddleRow)
            {
                grid = new GridNode(3, 2)
                    .WithColumnWidths(new SizeConstraint.Percent(60), new SizeConstraint.Fill())
                    .WithRowHeights(
                        new SizeConstraint.Percent(30),
                        new SizeConstraint.Percent(20),
                        new SizeConstraint.Fill());

                if (showCpu)
                {
                    grid.SetCell(0, 0, BuildCpuPanel(), colSpan: 2);
                }

                switch (showMem)
                {
                    case true when showNet:
                        grid.SetCell(1, 0, BuildMemoryPanel());
                        grid.SetCell(1, 1, BuildNetDiskPanel());
                        break;
                    case true:
                        grid.SetCell(1, 0, BuildMemoryPanel(), colSpan: 2);
                        break;
                    default:
                        {
                            if (showNet)
                            {
                                grid.SetCell(1, 0, BuildNetDiskPanel(), colSpan: 2);
                            }

                            break;
                        }
                }

                if (showProc)
                {
                    grid.SetCell(2, 0, BuildProcessPanel(), colSpan: 2);
                }
            }
            else
            {
                grid = new GridNode(2, 2)
                    .WithColumnWidths(new SizeConstraint.Percent(60), new SizeConstraint.Fill())
                    .WithRowHeights(
                        new SizeConstraint.Percent(30),
                        new SizeConstraint.Fill());

                if (showCpu)
                {
                    grid.SetCell(0, 0, BuildCpuPanel(), colSpan: 2);
                }

                if (showProc)
                {
                    grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 2);
                }
            }

            return grid;
        }
    }

    /// <summary>Preset 1 – CPU Focus: top CPU (full-width), bottom Processes (full-width)</summary>
    private ILayoutNode BuildPreset1CpuFocus()
    {
        var showCpu = ViewModel.ShowCpu.Value;
        var showProc = ViewModel.ShowProcesses.Value;

        var grid = new GridNode(2, 2)
            .WithColumnWidths(new SizeConstraint.Fill(), new SizeConstraint.Fill())
            .WithRowHeights(new SizeConstraint.Percent(50), new SizeConstraint.Fill());

        if (showCpu)
        {
            grid.SetCell(0, 0, BuildCpuPanel(), colSpan: 2);
        }

        if (showProc)
        {
            grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 2);
        }

        return grid;
    }

    /// <summary>Preset 2 – Resource Grid: 3-col resource row, then NET/DISK + Processes</summary>
    private ILayoutNode BuildPreset2ResourceGrid()
    {
        var showCpu = ViewModel.ShowCpu.Value;
        var showMem = ViewModel.ShowMemory.Value;
        var showNet = ViewModel.ShowNetDisk.Value;
        var showProc = ViewModel.ShowProcesses.Value;
        var showGpu = ViewModel.GpuAvailable && ViewModel.ShowGpu.Value;

        if (showGpu)
        {
            var grid = new GridNode(2, 3)
                .WithColumnWidths(new SizeConstraint.Fill(), new SizeConstraint.Fill(), new SizeConstraint.Fill())
                .WithRowHeights(new SizeConstraint.Percent(35), new SizeConstraint.Fill());

            // Row 0: CPU | Memory | GPU — place only visible panels, pack left
            var row0Col = 0;
            if (showCpu)
            {
                grid.SetCell(0, row0Col++, BuildCpuPanel());
            }

            if (showMem)
            {
                grid.SetCell(0, row0Col++, BuildMemoryPanel());
            }

            grid.SetCell(0, row0Col, BuildGpuPanel(), colSpan: 3 - row0Col);

            switch (showNet)
            {
                // Row 1: Net/Disk | Processes
                case true when showProc:
                    grid.SetCell(1, 0, BuildNetDiskPanel());
                    grid.SetCell(1, 1, BuildProcessPanel(), colSpan: 2);
                    break;
                case true:
                    grid.SetCell(1, 0, BuildNetDiskPanel(), colSpan: 3);
                    break;
                default:
                    {
                        if (showProc)
                        {
                            grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 3);
                        }

                        break;
                    }
            }

            return grid;
        }
        else
        {
            var grid = new GridNode(2, 3)
                .WithColumnWidths(new SizeConstraint.Fill(), new SizeConstraint.Fill(), new SizeConstraint.Fill())
                .WithRowHeights(new SizeConstraint.Percent(35), new SizeConstraint.Fill());

            // Row 0: CPU | Memory | Net/Disk — place only visible panels, pack left
            var row0Col = 0;
            if (showCpu)
            {
                grid.SetCell(0, row0Col++, BuildCpuPanel());
            }

            if (showMem)
            {
                grid.SetCell(0, row0Col++, BuildMemoryPanel());
            }

            if (showNet)
            {
                grid.SetCell(0, row0Col, BuildNetDiskPanel());
            }

            if (showProc)
            {
                grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 3);
            }

            return grid;
        }
    }

    /// <summary>Preset 3 – Minimal: compact CPU strip, then Processes</summary>
    private ILayoutNode BuildPreset3Minimal()
    {
        var showCpu = ViewModel.ShowCpu.Value;
        var showProc = ViewModel.ShowProcesses.Value;

        var grid = new GridNode(2, 1)
            .WithColumnWidths(new SizeConstraint.Fill())
            .WithRowHeights(new SizeConstraint.Percent(15), new SizeConstraint.Fill());

        if (showCpu)
        {
            grid.SetCell(0, 0, BuildCpuStripPanel());
        }

        if (showProc)
        {
            grid.SetCell(1, 0, BuildProcessPanel());
        }

        return grid;
    }

    // ── Panel builders ───────────────────────────────────────────────────────

    private ILayoutNode BuildCpuPanel()
    {
        var theme = _theme.Current;
        _cpuBox = new BtopBoxNode()
            .WithTitle("cpu")
            .WithHotkey(1)
            .WithBorderColor(theme.Accent)
            .WithTitleColor(theme.PanelTitle)
            .WithHighlightColor(theme.Accent)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.CpuTotal
                            .Select<double, ILayoutNode>(pct =>
                                new TextNode($" {ViewModel.CpuName.Value}  —  Total {pct:F1}%")
                                    .WithForeground(theme.Accent))
                            .AsLayout().Height(1))
                    .WithChild(_coresNode!)
                    .WithChild(_cpuGraph!.Fill()));
        return _cpuBox.Fill();
    }

    private ILayoutNode BuildCpuStripPanel()
    {
        var theme = _theme.Current;
        return new BtopBoxNode()
            .WithTitle("cpu")
            .WithHotkey(1)
            .WithBorderColor(theme.Accent)
            .WithTitleColor(theme.PanelTitle)
            .WithHighlightColor(theme.Accent)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(ViewModel.CpuTotal
                        .Select<double, ILayoutNode>(pct =>
                            new TextNode($" Total {pct:F1}%  {ViewModel.CpuName.Value}")
                                .WithForeground(theme.Accent)).AsLayout().Height(1))
                    .WithChild(ViewModel.CpuTotal
                        .Select<double, ILayoutNode>(pct => new ProgressBarNode()
                            .WithRange(0, 100)
                            .WithValue(pct)
                            .WithGradient(BtopGradients.Resource(theme))
                            .WithFillChar(BtopGradients.MeterFill)
                            .WithEmptyChar(BtopGradients.MeterEmpty))
                        .AsLayout().Height(1)))
            .Fill();
    }

    private ILayoutNode BuildMemoryPanel()
    {
        var theme = _theme.Current;
        return new BtopBoxNode()
            .WithTitle("mem")
            .WithHotkey(2)
            .WithBorderColor(theme.Warning)
            .WithTitleColor(theme.PanelTitle)
            .WithHighlightColor(theme.Accent)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.RamUsed.CombineLatest<ulong, ulong, ILayoutNode>(ViewModel.RamTotal,
                            (used, total) =>
                            {
                                var usedGb = used / 1024.0 / 1024 / 1024;
                                var totalGb = total / 1024.0 / 1024 / 1024;
                                var pct = total > 0 ? (double)used / total * 100 : 0;
                                return new TextNode($" {usedGb:F1} / {totalGb:F1} GiB  {pct:F1}%")
                                    .WithForeground(theme.Foreground);
                            }).AsLayout().Height(1))
                    .WithChild(
                        ViewModel.RamUsed.CombineLatest<ulong, ulong, ILayoutNode>(ViewModel.RamTotal,
                            (used, total) =>
                            {
                                var pct = total > 0 ? (double)used / total * 100 : 0;
                                return new ProgressBarNode()
                                    .WithRange(0, 100).WithValue(pct)
                                    .WithGradient(BtopGradients.Resource(theme))
                                    .WithFillChar(BtopGradients.MeterFill)
                                    .WithEmptyChar(BtopGradients.MeterEmpty);
                            }).AsLayout().Height(1))
                    .WithChild(_ramGraph!.Fill())
                    .Fill())
            .Fill();
    }

    private ILayoutNode BuildGpuPanel()
    {
        var theme = _theme.Current;
        return new BtopBoxNode()
            .WithTitle("gpu")
            .WithHotkey(5)
            .WithBorderColor(theme.PanelTitle)
            .WithTitleColor(theme.PanelTitle)
            .WithHighlightColor(theme.Accent)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.Gpu
                            .Select<GpuSnapshot?, ILayoutNode>(gpu =>
                            {
                                if (gpu is null)
                                {
                                    return new TextNode(" No GPU data").WithForeground(theme.TextDim);
                                }

                                var vramMb = gpu.VramUsedBytes / 1024.0 / 1024;
                                var vramTotalMb = gpu.VramTotalBytes / 1024.0 / 1024;
                                return Layouts.Vertical()
                                    .WithChild(new TextNode($" {gpu.Name}").WithForeground(theme.PanelTitle).Height(1))
                                    .WithChild(new TextNode(
                                            $" Usage {gpu.UsagePercent:F0}%  Temp {gpu.TemperatureCelsius:F0}°C")
                                        .WithForeground(theme.Foreground).Height(1))
                                    .WithChild(new TextNode($" VRAM {vramMb:F0}/{vramTotalMb:F0}MB")
                                        .WithForeground(theme.Foreground).Height(1));
                            }).AsLayout())
                    .WithChild(_gpuGraph!.Fill()))
            .Fill();
    }

    private ILayoutNode BuildNetDiskPanel()
    {
        var theme = _theme.Current;
        return new BtopBoxNode()
            .WithTitle("net")
            .WithHotkey(3)
            .WithBorderColor(theme.Success)
            .WithTitleColor(theme.PanelTitle)
            .WithHighlightColor(theme.Accent)
            .WithContent(
                ViewModel.Networks.CombineLatest<IReadOnlyList<NetworkSnapshot>, IReadOnlyList<DiskSnapshot>, ILayoutNode>(
                    ViewModel.Disks,
                    (nets, disks) =>
                    {
                        var layout = Layouts.Vertical();

                        var topNets = nets
                            .OrderByDescending(n => n.RxBytesPerSec + n.TxBytesPerSec)
                            .Take(2)
                            .ToList();

                        foreach (var net in topNets)
                        {
                            var name = net.Name.Length > 12 ? net.Name[..11] + "…" : net.Name;
                            layout.WithChild(new TextNode(
                                    $" {name,-12} ↓{FormatBytes(net.RxBytesPerSec),9}  ↑{FormatBytes(net.TxBytesPerSec),9}")
                                .WithForeground(net.RxBytesPerSec > 0 || net.TxBytesPerSec > 0 ? theme.Foreground : theme.TextDim)
                                .Height(1));
                        }

                        if (topNets.Count > 0 && disks.Count > 0)
                        {
                            layout.WithChild(new TextNode("").Height(1));
                        }

                        foreach (var disk in disks)
                        {
                            var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
                            var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;
                            layout.WithChild(new TextNode(
                                    $" {disk.Name,-4} {usedGb:F0}/{totalGb:F0}GB")
                                .WithForeground(theme.Foreground).Height(1));
                            layout.WithChild(new ProgressBarNode()
                                .WithRange(0, 100)
                                .WithValue(disk.UsedPercent)
                                .WithGradient(BtopGradients.Resource(theme))
                                .WithFillChar(BtopGradients.MeterFill)
                                .WithEmptyChar(BtopGradients.MeterEmpty)
                                .WithLabel("{0:P0}")
                                .Height(1));
                        }

                        if (topNets.Count == 0 && disks.Count == 0)
                        {
                            layout.WithChild(new TextNode(" No active adapters").WithForeground(theme.TextDim));
                        }

                        return layout;
                    }).AsLayout())
            .Fill();
    }

    private ILayoutNode BuildProcessPanel()
    {
        var theme = _theme.Current;

        // Header re-renders whenever the sort column or direction changes so the
        // active column's ▲/▼ marker stays in sync.
        var header = ViewModel.SortField
            .CombineLatest(ViewModel.SortDescending, (_, _) => ViewModel.BuildProcessHeader())
            .Select<string, ILayoutNode>(text => new TextNode(text).WithForeground(theme.Header))
            .AsLayout().Height(1);

        // btop-style inline filter line: shown at the top of the proc box while
        // editing (with a block cursor) or when a filter is applied, so the typed
        // query is visible in context — not just buried in the bottom status line.
        var filterLine = ViewModel.IsFilterMode
            .CombineLatest(ViewModel.ProcessFilter, (editing, text) => (editing, text))
            .Select<(bool editing, string text), ILayoutNode>(s =>
            {
                if (s.editing)
                {
                    return new TextNode($" Filter: {s.text}█  [Enter] Apply  [Esc] Clear")
                        .WithForeground(theme.Accent).Height(1);
                }

                if (!string.IsNullOrEmpty(s.text))
                {
                    return new TextNode($" Filter: {s.text}  [f] Edit")
                        .WithForeground(theme.Accent).Height(1);
                }

                return new TextNode("").Height(0);
            })
            .AsLayout();

        return new BtopBoxNode()
            .WithTitle("proc")
            .WithHotkey(4)
            .WithBorderColor(theme.Accent)
            .WithTitleColor(theme.PanelTitle)
            .WithHighlightColor(theme.Accent)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(filterLine)
                    .WithChild(header)
                    .WithChild(_processList!.Fill()))
            .Fill();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FormatBytes(ulong bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB/s",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB/s",
        >= 1_024 => $"{bytes / 1_024.0:F1} KB/s",
        _ => $"{bytes} B/s",
    };
}
