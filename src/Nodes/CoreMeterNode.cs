using R3;
using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace Puhu.Btop.Nodes;

/// <summary>
/// Renders per-core CPU usage as a grid of btop-style gradient block meters
/// (<c>C0 ■■■·· 47%</c>), one core per cell, packed into columns by available width.
/// Replaces the old text-only CpuCoresNode.
/// </summary>
public sealed class CoreMeterNode : LayoutNode, IInvalidatingNode
{
    private const int MeterWidth = 6;  // filled+empty cells in each core's bar
    private const int ItemWidth = 16;  // " C12 ■■■■■■ 100% "

    private readonly Subject<Unit> _invalidated = new();
    private IReadOnlyList<double> _cores = [];
    private Gradient _gradient = Gradient.Create(Color.Green, Color.Yellow, Color.Red);
    private int _rowCount = 1;
    private bool _disposed;

    public Observable<Unit> Invalidated => _invalidated.AsObservable();

    public CoreMeterNode WithGradient(Gradient gradient) { _gradient = gradient; return this; }

    public void SetCores(IReadOnlyList<double> cores)
    {
        _cores = cores;
        if (!_disposed) _invalidated.OnNext(Unit.Default);
    }

    public override Size Measure(Size available)
    {
        if (_cores.Count == 0)
        {
            _rowCount = 0;
            return available with { Height = 0 };
        }

        var perRow = Math.Max(1, available.Width / ItemWidth);
        _rowCount = (int)Math.Ceiling((double)_cores.Count / perRow);
        var width = WidthConstraint.Compute(available.Width, available.Width, available.Width);
        return new Size(width, _rowCount);
    }

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea || _cores.Count == 0) return;

        var ctx = context.CreateSubContext(bounds);
        var perRow = Math.Max(1, bounds.Width / ItemWidth);

        for (var i = 0; i < _cores.Count; i++)
        {
            var row = i / perRow;
            var col = i % perRow;
            if (row >= bounds.Height) break;

            var x = col * ItemWidth;
            var pct = Math.Clamp(_cores[i], 0, 100);

            // "C<i> "
            ctx.SetForeground(Color.Gray);
            var label = $"C{i} ";
            ctx.WriteAt(x, row, label);
            var bx = x + label.Length;

            // gradient meter
            var filled = (int)Math.Round(pct / 100.0 * MeterWidth);
            for (var c = 0; c < MeterWidth; c++)
            {
                if (c < filled)
                {
                    var t = MeterWidth > 1 ? c / (float)(MeterWidth - 1) : 0f;
                    ctx.SetForeground(_gradient.Sample(t));
                    ctx.WriteAt(bx + c, row, BtopGradients.MeterFill);
                }
                else
                {
                    ctx.SetForeground(Color.DarkGray);
                    ctx.WriteAt(bx + c, row, BtopGradients.MeterEmpty);
                }
            }

            // " <pct>%"
            ctx.SetForeground(_gradient.Sample((float)(pct / 100.0)));
            ctx.WriteAt(bx + MeterWidth, row, $" {pct,3:F0}%");
        }

        ctx.ResetColors();
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _invalidated.OnCompleted();
        _invalidated.Dispose();
        base.Dispose();
    }
}
