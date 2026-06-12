using R3;
using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace Puhu.Btop.Nodes;

public interface IScrollableList
{
    void MoveUp();
    void MoveDown();
    void MoveToTop();
    void MoveToEnd();
    void PageUp();
    void PageDown();
}

public sealed class DataListNode<T>(Func<T, string> formatter, Func<T, Color>? colorSelector = null)
    : LayoutNode, IInvalidatingNode, IScrollableList
{
    private readonly Subject<Unit> _invalidated = new();

    private IReadOnlyList<T> _items = [];
    private int _scrollOffset;
    private int _viewportHeight = 20;
    private bool _disposed;

    private Color _selectedFg = Color.Black;
    private Color _selectedBg = Color.Cyan;
    private Color _background = Color.Default;
    private Color _foreground = Color.White;
    private Color _scrollbarTrack = Color.Gray;

    public Observable<Unit> Invalidated => _invalidated.AsObservable();

    public int SelectedIndex { get; private set; }

    public T? SelectedItem => SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : default;

    public DataListNode<T> WithHighlightColors(Color fg, Color bg)
    {
        _selectedFg = fg;
        _selectedBg = bg;
        return this;
    }

    public void SetItems(IReadOnlyList<T> items)
    {
        _items = items;
        if (SelectedIndex >= _items.Count)
        {
            SelectedIndex = Math.Max(0, _items.Count - 1);
        }

        Invalidate();
    }

    public void MoveUp()
    {
        if (SelectedIndex > 0)
        {
            SelectedIndex--;
            EnsureVisible();
            Invalidate();
        }
    }

    public void MoveDown()
    {
        if (SelectedIndex < _items.Count - 1)
        {
            SelectedIndex++;
            EnsureVisible();
            Invalidate();
        }
    }

    public void MoveToTop()
    {
        SelectedIndex = 0;
        _scrollOffset = 0;
        Invalidate();
    }

    public void MoveToEnd()
    {
        SelectedIndex = Math.Max(0, _items.Count - 1);
        EnsureVisible();
        Invalidate();
    }

    public void PageUp()
    {
        SelectedIndex = Math.Max(0, SelectedIndex - _viewportHeight);
        EnsureVisible();
        Invalidate();
    }

    public void PageDown()
    {
        SelectedIndex = Math.Min(Math.Max(0, _items.Count - 1), SelectedIndex + _viewportHeight);
        EnsureVisible();
        Invalidate();
    }

    private void EnsureVisible()
    {
        if (SelectedIndex < _scrollOffset)
        {
            _scrollOffset = SelectedIndex;
        }
        else if (SelectedIndex >= _scrollOffset + _viewportHeight)
        {
            _scrollOffset = SelectedIndex - _viewportHeight + 1;
        }
    }

    public override Size Measure(Size available)
    {
        _viewportHeight = HeightConstraint.Compute(available.Height, _items.Count, available.Height);
        var width = WidthConstraint.Compute(available.Width, available.Width, available.Width);
        return new Size(width, _viewportHeight);
    }

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea)
        {
            return;
        }

        _viewportHeight = bounds.Height;
        var ctx = context.CreateSubContext(bounds);

        if (_background != Color.Default)
        {
            ctx.SetBackground(_background);
            ctx.Fill(0, 0, bounds.Width, bounds.Height);
        }

        var showScrollbar = _items.Count > _viewportHeight;
        var contentWidth = showScrollbar ? bounds.Width - 1 : bounds.Width;

        for (var row = 0; row < _viewportHeight; row++)
        {
            var itemIdx = _scrollOffset + row;
            if (itemIdx >= _items.Count)
            {
                ctx.Fill(0, row, contentWidth, 1);
                continue;
            }

            var item = _items[itemIdx];
            var text = formatter(item);
            if (text.Length > contentWidth)
            {
                text = text[..(contentWidth - 1)] + "…";
            }
            else if (text.Length < contentWidth)
            {
                text = text.PadRight(contentWidth);
            }

            if (itemIdx == SelectedIndex)
            {
                ctx.SetForeground(_selectedFg);
                ctx.SetBackground(_selectedBg);
            }
            else if (colorSelector is not null)
            {
                ctx.SetForeground(colorSelector(item));
                if (_background != Color.Default)
                {
                    ctx.SetBackground(_background);
                }
            }
            else if (_background != Color.Default)
            {
                ctx.SetBackground(_background);
            }

            ctx.WriteAt(0, row, text);
            ctx.ResetColors();
        }

        if (showScrollbar)
        {
            RenderScrollbar(ctx, bounds.Width - 1, _viewportHeight);
        }
    }

    private void RenderScrollbar(IRenderContext ctx, int x, int height)
    {
        var totalItems = _items.Count;
        if (totalItems <= height)
        {
            return;
        }

        var thumbSize = Math.Max(1, height * height / totalItems);
        var maxThumbTop = height - thumbSize;
        var maxScroll = Math.Max(1, totalItems - height);
        var thumbTop = (int)((float)_scrollOffset / maxScroll * maxThumbTop);

        for (var row = 0; row < height; row++)
        {
            var isThumb = row >= thumbTop && row < thumbTop + thumbSize;
            ctx.SetForeground(isThumb ? _foreground : _scrollbarTrack);
            ctx.WriteAt(x, row, isThumb ? '█' : '░');
        }
        ctx.ResetColors();
    }

    private void Invalidate()
    {
        if (!_disposed)
        {
            _invalidated.OnNext(Unit.Default);
        }
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
