using Puhu.Btop.Nodes;
using Termina.Layout;
using Termina.Terminal;

namespace Puhu.Btop.Tests.Nodes;

public class CoreMeterNodeTests
{
    [Fact]
    public void Empty_cores_measure_to_zero_height()
    {
        var node = new CoreMeterNode();
        var size = node.Measure(new Size(40, 5));
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Renders_label_meter_and_percent_per_core()
    {
        var node = new CoreMeterNode().WithGradient(Gradient.Create(Color.Green, Color.Red));
        node.SetCores([47.0, 88.0]);

        var ctx = new TestRenderContext(40, 4);
        node.Measure(new Size(40, 4));
        node.Render(ctx, new Rect(0, 0, 40, 4));

        var all = string.Concat(ctx.Cells.OrderBy(c => c.Y).ThenBy(c => c.X).Select(c => c.Text));
        Assert.Contains("C0", all);
        Assert.Contains("47%", all);
        Assert.Contains("C1", all);
        Assert.Contains("88%", all);
        Assert.Contains(BtopGradients.MeterFill.ToString(), all);
    }

    [Fact]
    public void Higher_percent_fills_more_cells_than_lower_percent()
    {
        var fill = BtopGradients.MeterFill;

        var low = new CoreMeterNode();
        low.SetCores([10.0]);
        var lowCtx = new TestRenderContext(40, 4);
        low.Measure(new Size(40, 4));
        low.Render(lowCtx, new Rect(0, 0, 40, 4));
        var lowFill = lowCtx.Cells.SelectMany(c => c.Text).Count(ch => ch == fill);

        var high = new CoreMeterNode();
        high.SetCores([90.0]);
        var highCtx = new TestRenderContext(40, 4);
        high.Measure(new Size(40, 4));
        high.Render(highCtx, new Rect(0, 0, 40, 4));
        var highFill = highCtx.Cells.SelectMany(c => c.Text).Count(ch => ch == fill);

        Assert.True(highFill > lowFill);
    }
}
