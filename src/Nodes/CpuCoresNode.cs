using R3;
using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace Puhu.Btop.Nodes;

public sealed class CpuCoresNode : LayoutNode, IInvalidatingNode
{
    private readonly Subject<Unit> _invalidated = new();
    private IReadOnlyList<double> _cores = [];
    private int _rowCount = 1;
    private bool _disposed;

    private const int ItemWidth = 10; // " C0: 45% " = ~10 chars

    public Observable<Unit> Invalidated => _invalidated.AsObservable();

    public void SetCores(IReadOnlyList<double> cores)
    {
        _cores = cores;
        if (!_disposed)
        {
            _invalidated.OnNext(Unit.Default);
        }
    }

    public override Size Measure(Size available)
    {
        if (_cores.Count == 0)
        {
            _rowCount = 0;
            return available with { Height = 0 };
        }

        var coresPerRow = Math.Max(1, available.Width / ItemWidth);
        _rowCount = (int)Math.Ceiling((double)_cores.Count / coresPerRow);

        var width = WidthConstraint.Compute(available.Width, available.Width, available.Width);
        return new Size(width, _rowCount);
    }

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea || _cores.Count == 0)
        {
            return;
        }

        var ctx = context.CreateSubContext(bounds);
        ctx.Fill(0, 0, bounds.Width, bounds.Height);

        var coresPerRow = Math.Max(1, bounds.Width / ItemWidth);

        for (var i = 0; i < _cores.Count; i++)
        {
            var row = i / coresPerRow;
            var col = i % coresPerRow;
            if (row >= bounds.Height)
            {
                break;
            }

            var x = col * ItemWidth;
            var text = $" C{i}:{_cores[i],3:F0}% ";
            if (x + text.Length > bounds.Width)
            {
                text = text[..(bounds.Width - x)];
            }

            ctx.SetForeground(Color.Cyan);
            ctx.WriteAt(x, row, text);
        }
        ctx.ResetColors();
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _invalidated.OnCompleted();
        _invalidated.Dispose();
        base.Dispose();
    }
}
