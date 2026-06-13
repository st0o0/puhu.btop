using Puhu.Btop.Nodes;
using Termina.Layout;
using Termina.Terminal;

namespace Puhu.Btop.Tests.Nodes;

public class BtopBoxNodeTests
{
    private static (TestRenderContext ctx, BtopBoxNode box) Render(BtopBoxNode box, int w, int h)
    {
        var ctx = new TestRenderContext(w, h);
        box.Measure(new Size(w, h));
        box.Render(ctx, new Rect(0, 0, w, h));
        return (ctx, box);
    }

    [Fact]
    public void Renders_rounded_corners()
    {
        var (ctx, _) = Render(new BtopBoxNode(), 10, 3);
        Assert.Equal('╭', ctx.Row(0)[0]);
        Assert.Equal('╮', ctx.Row(0)[^1]);
        Assert.Equal('╰', ctx.Row(2)[0]);
        Assert.Equal('╯', ctx.Row(2)[^1]);
    }

    [Fact]
    public void Renders_title_with_brackets_and_superscript_hotkey_at_offset_two()
    {
        var box = new BtopBoxNode().WithTitle("cpu").WithHotkey(1);
        var (ctx, _) = Render(box, 20, 3);

        var top = ctx.Row(0);
        // offset 2='┤', 3='¹', 4..6='cpu', 7='├'
        Assert.Equal("┤¹cpu├", top.Substring(2, 6));
    }

    [Fact]
    public void Hotkey_digit_uses_highlight_color()
    {
        var box = new BtopBoxNode().WithTitle("cpu").WithHotkey(1)
            .WithHighlightColor(Color.Yellow);
        var (ctx, _) = Render(box, 20, 3);

        Assert.Equal(Color.Yellow, ctx.ColorAt(3, 0)); // superscript cell
    }

    [Fact]
    public void Title_without_hotkey_starts_directly_after_left_bracket()
    {
        // No hotkey → no superscript glyph; title sits right after ┤
        var box = new BtopBoxNode().WithTitle("net");
        var (ctx, _) = Render(box, 20, 3);
        Assert.Equal("┤net├", ctx.Row(0).Substring(2, 5));
    }

    [Fact]
    public void Empty_bounds_do_not_throw()
    {
        var box = new BtopBoxNode().WithTitle("cpu");
        box.Render(new TestRenderContext(0, 0), new Rect(0, 0, 0, 0));
    }

    [Fact]
    public void Content_renders_inside_the_border()
    {
        var box = new BtopBoxNode().WithContent(new TextNode("X"));
        var (ctx, _) = Render(box, 10, 3);

        // Inner content origin (0,0) maps to outer (1,1), just inside the top-left border.
        Assert.Equal("X", ctx.Row(1).Substring(1, 1));
    }

    [Fact]
    public void Center_title_is_drawn_centered_in_top_border()
    {
        var box = new BtopBoxNode().WithTitle("cpu").WithHotkey(1).WithCenterTitle("14:32:07");
        var (ctx, _) = Render(box, 40, 3);

        var top = ctx.Row(0);
        Assert.Contains("┤ 14:32:07 ├", top);
    }

    [Fact]
    public void Center_title_is_omitted_when_box_too_narrow()
    {
        var box = new BtopBoxNode().WithTitle("cpu").WithHotkey(1).WithCenterTitle("14:32:07");
        var (ctx, _) = Render(box, 16, 3);

        Assert.DoesNotContain("14:32:07", ctx.Row(0));
    }

    [Fact]
    public void Title_without_hotkey_is_not_over_truncated()
    {
        // Width 9: ╭─┤proc├╮ — "proc" (4 chars) must appear untruncated.
        // Without hotkey the bracket overhead is only ┤├ (2 chars), so the max title
        // length is w-5 = 4, which fits "proc" exactly.  The old w-6 = 3 would have
        // clipped it to "pro".
        // Layout: col 0=╭ col 1=─ col 2=┤ col 3..6=proc col 7=├ col 8=╮
        var box = new BtopBoxNode().WithTitle("proc");
        var (ctx, _) = Render(box, 9, 3);
        Assert.Equal("┤proc├", ctx.Row(0).Substring(2, 6));
    }
}
