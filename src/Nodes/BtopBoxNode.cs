using R3;
using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace Puhu.Btop.Nodes;

/// <summary>
/// A btop-style bordered box: rounded corners, a title drawn into the top border as
/// <c>┤&lt;hotkey&gt;&lt;title&gt;├</c> at column offset 2 with a superscript hotkey digit in the
/// highlight color, plus an optional centered title segment (e.g. a clock) and content.
/// Replaces <see cref="Termina.Layout.PanelNode"/>, which is sealed and cannot express this.
/// </summary>
/// <remarks>
/// Known limitation (accepted for the visual sub-project): because Terminas
/// <c>GetChildNodes()</c> and <c>DisconnectChildInvalidationSubscriptions()</c> are
/// <c>internal virtual</c> and not overridable from this assembly, the framework's tree
/// walker does not see this box's content. Rendering and reactive re-rendering work
/// (content is rendered directly and its <see cref="IInvalidatingNode.Invalidated"/> is
/// forwarded), but two framework features are blind to content nested in this box:
/// focus traversal (no focusable content is used yet) and abandoned-subtree subscription
/// cleanup on layout rebuilds (a low-frequency, per-interaction subscription leak).
/// Proper fix when we next touch the Termina submodule / add focusable box content:
/// add <c>[assembly: InternalsVisibleTo("Puhu.Btop")]</c> to Termina and override both
/// methods like <c>PanelNode</c> does.
/// </remarks>
public sealed class BtopBoxNode : LayoutNode, IInvalidatingNode
{
    private const char TopLeft = '╭', TopRight = '╮', BottomLeft = '╰', BottomRight = '╯';
    private const char H = '─', V = '│', TitleLeft = '┤', TitleRight = '├';

    // Superscript digits 1..9 (index 1..9; index 0 unused).
    private static readonly char[] Superscript =
        ['⁰', '¹', '²', '³', '⁴', '⁵', '⁶', '⁷', '⁸', '⁹'];

    private readonly Subject<Unit> _invalidated = new();
    private IDisposable? _contentSub;
    private ILayoutNode _content = new EmptyNode();

    private string? _title;
    private int _hotkey;            // 0 = none
    private string? _centerTitle;
    private Color? _borderColor;
    private Color? _titleColor;
    private Color? _highlightColor;

    public Observable<Unit> Invalidated => _invalidated;

    public BtopBoxNode()
    {
        HeightConstraint = new SizeConstraint.Fill();
        WidthConstraint = new SizeConstraint.Fill();
    }

    public BtopBoxNode WithTitle(string title) { _title = title; return this; }
    public BtopBoxNode WithHotkey(int hotkey) { _hotkey = hotkey; return this; }
    public BtopBoxNode WithCenterTitle(string? centerTitle) { _centerTitle = centerTitle; _invalidated.OnNext(Unit.Default); return this; }
    public BtopBoxNode WithBorderColor(Color color) { _borderColor = color; return this; }
    public BtopBoxNode WithTitleColor(Color color) { _titleColor = color; return this; }
    public BtopBoxNode WithHighlightColor(Color color) { _highlightColor = color; return this; }

    public BtopBoxNode WithContent(ILayoutNode content)
    {
        _contentSub?.Dispose();
        _content.Dispose();
        _content = content;
        if (content is IInvalidatingNode inv)
        {
            _contentSub = inv.Invalidated.Subscribe(_ => _invalidated.OnNext(Unit.Default));
        }
        return this;
    }

    public override Size Measure(Size available)
    {
        var inner = available.Shrink(2, 2);
        _content.Measure(inner);
        var w = WidthConstraint.Compute(available.Width, available.Width, available.Width);
        var h = HeightConstraint.Compute(available.Height, available.Height, available.Height);
        return new Size(w, h);
    }

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea || bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }

        var ctx = context.CreateSubContext(bounds);
        var w = bounds.Width;
        var h = bounds.Height;

        void Border(Color? c)
        {
            if (c.HasValue) ctx.SetForeground(c.Value);
            else ctx.ResetColors();
        }

        // Top-left corner.
        Border(_borderColor);
        ctx.WriteAt(0, 0, TopLeft);

        // Title segment at offset 2: ┤<superscript-hotkey><title>├
        // Write top border in segments so each character lands at the exact column
        // the TestRenderContext.Row() helper expects when it sorts by X.
        var titleEnd = 2;
        if (!string.IsNullOrEmpty(_title) && w > 6)
        {
            var maxTitle = _hotkey is >= 1 and <= 9 ? w - 6 : w - 5;
            var title = _title!.Length > maxTitle ? _title[..maxTitle] : _title!;

            // Segment 1: single dash between corner and left bracket.
            ctx.WriteAt(1, 0, H);

            Border(_borderColor);
            ctx.WriteAt(2, 0, TitleLeft);
            var x = 3;

            if (_hotkey is >= 1 and <= 9)
            {
                if (_highlightColor.HasValue) ctx.SetForeground(_highlightColor.Value);
                ctx.WriteAt(x, 0, Superscript[_hotkey]);
                x++;
            }

            if (_titleColor.HasValue) ctx.SetForeground(_titleColor.Value);
            else ctx.ResetColors();
            ctx.WriteAt(x, 0, title);
            x += title.Length;

            Border(_borderColor);
            ctx.WriteAt(x, 0, TitleRight);
            titleEnd = x + 1;

            // Remaining dashes from titleEnd to w-2, then corner.
            var remaining = w - 1 - titleEnd;
            if (remaining > 0) ctx.WriteAt(titleEnd, 0, new string(H, remaining));
        }
        else
        {
            // No title: single dash run from col 1 to col w-2.
            if (w > 2) ctx.WriteAt(1, 0, new string(H, w - 2));
        }

        ctx.WriteAt(w - 1, 0, TopRight);

        // Centered title segment (e.g. clock): ┤ <text> ├, only if it fits clear of the title.
        if (!string.IsNullOrEmpty(_centerTitle))
        {
            var seg = $"{TitleLeft} {_centerTitle} {TitleRight}";
            var start = (w / 2) - (seg.Length / 2);
            if (start > titleEnd && start + seg.Length < w - 1)
            {
                Border(_borderColor);
                ctx.WriteAt(start, 0, seg);
            }
        }

        // Sides.
        Border(_borderColor);
        for (var y = 1; y < h - 1; y++)
        {
            ctx.WriteAt(0, y, V);
            ctx.WriteAt(w - 1, y, V);
        }

        // Bottom border.
        ctx.WriteAt(0, h - 1, BottomLeft);
        if (w > 2) ctx.WriteAt(1, h - 1, new string(H, w - 2));
        ctx.WriteAt(w - 1, h - 1, BottomRight);

        ctx.ResetColors();

        // Content inside the border.
        var contentBounds = new Rect(1, 1, w - 2, h - 2);
        if (contentBounds.HasArea)
        {
            var contentCtx = ctx.CreateSubContext(contentBounds);
            _content.Render(contentCtx, new Rect(0, 0, contentBounds.Width, contentBounds.Height));
        }
    }

    public override void OnActivate()
    {
        if (_content is IActivatableNode a) a.OnActivate();
        base.OnActivate();
    }

    public override void OnDeactivate()
    {
        if (_content is IActivatableNode a) a.OnDeactivate();
        base.OnDeactivate();
    }

    public override void Dispose()
    {
        _contentSub?.Dispose();
        _invalidated.OnCompleted();
        _invalidated.Dispose();
        _content.Dispose();
        base.Dispose();
    }
}
