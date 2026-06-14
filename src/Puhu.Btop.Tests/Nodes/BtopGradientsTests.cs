using Puhu.Btop.Nodes;
using Puhu.Plugin;
using Termina.Terminal;

namespace Puhu.Btop.Tests.Nodes;

public class BtopGradientsTests
{
    [Fact(Timeout = 30000)]
    public void Resource_samples_success_at_zero_and_error_at_one()
    {
        var theme = new ThemeDefinition
        {
            Success = Color.Green,
            Warning = Color.Yellow,
            Error = Color.Red,
        };

        var gradient = BtopGradients.Resource(theme);

        Assert.Equal(Color.Green, gradient.Sample(0f));
        Assert.Equal(Color.Red, gradient.Sample(1f));
    }

    [Fact(Timeout = 30000)]
    public void MeterFill_and_MeterEmpty_are_the_btop_glyphs()
    {
        Assert.Equal('■', BtopGradients.MeterFill);  // U+25A0
        Assert.Equal('·', BtopGradients.MeterEmpty); // U+00B7
    }
}
