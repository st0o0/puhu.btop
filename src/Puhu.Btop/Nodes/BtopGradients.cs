using Puhu.Plugin;
using Termina.Terminal;

namespace Puhu.Btop.Nodes;

/// <summary>
/// Btop-style gradients and meter glyphs shared by the per-core meter, the RAM/disk
/// meters and the graphs, so meters and graphs colorize identically.
/// </summary>
public static class BtopGradients
{
    /// <summary>Filled meter cell glyph (■).</summary>
    public const char MeterFill = '■';

    /// <summary>Empty meter cell glyph (·).</summary>
    public const char MeterEmpty = '·';

    /// <summary>
    /// Resource-usage gradient green→yellow→red derived from the theme
    /// (low = healthy, high = critical), matching btop's meter coloring.
    /// </summary>
    public static Gradient Resource(ThemeDefinition theme) =>
        Gradient.Create(theme.Success, theme.Warning, theme.Error);
}
