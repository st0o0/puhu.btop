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
        var node = new CoreMeterNode();
        node.SetCores([10.0, 90.0]);

        var ctx = new TestRenderContext(40, 4);
        node.Measure(new Size(40, 4));
        node.Render(ctx, new Rect(0, 0, 40, 4));

        var fill = BtopGradients.MeterFill;
        var core0Fill = ctx.Cells.Where(c => c.Y == 0).SelectMany(c => c.Text).Count(ch => ch == fill);
        var core1Fill = ctx.Cells.Where(c => c.Y == 1).SelectMany(c => c.Text).Count(ch => ch == fill);
        Assert.True(core1Fill > core0Fill);
    }
}
