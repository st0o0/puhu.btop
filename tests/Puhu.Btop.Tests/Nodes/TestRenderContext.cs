using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace Puhu.Btop.Tests.Nodes;

/// <summary>
/// In-memory <see cref="IRenderContext"/> that records every written cell and the
/// foreground color active at write time, so node rendering can be asserted.
/// </summary>
public sealed class TestRenderContext : IRenderContext
{
    private Color? _fg;

    public List<(int X, int Y, string Text)> Cells { get; } = [];
    public List<(int X, int Y, Color Color)> Colors { get; } = [];

    public TestRenderContext(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public void WriteAt(int x, int y, char c) => WriteAt(x, y, c.ToString());

    public void WriteAt(int x, int y, string text)
    {
        Cells.Add((x, y, text));
        if (_fg.HasValue)
        {
            Colors.Add((x, y, _fg.Value));
        }
    }

    public void SetForeground(Color color) => _fg = color;
    public void SetBackground(Color color) { }
    public void ResetColors() => _fg = null;
    public void SetDecoration(TextDecoration decoration) { }
    public void ApplyStyle(TextStyle style) { }
    public void Fill(int x, int y, int width, int height, char c = ' ') { }
    public void Clear() { }

    public IRenderContext CreateSubContext(Rect bounds) => new SubContext(this, bounds);

    /// <summary>Concatenate every fragment written to row <paramref name="y"/>, ordered by column.</summary>
    public string Row(int y) =>
        string.Concat(Cells.Where(c => c.Y == y).OrderBy(c => c.X).Select(c => c.Text));

    /// <summary>The foreground color recorded for the first write that starts at (x,y), if any.</summary>
    public Color? ColorAt(int x, int y) =>
        Colors.Where(c => c.X == x && c.Y == y).Select(c => (Color?)c.Color).FirstOrDefault();

    private sealed class SubContext : IRenderContext
    {
        private readonly TestRenderContext _parent;
        private readonly Rect _bounds;

        public SubContext(TestRenderContext parent, Rect bounds)
        {
            _parent = parent;
            _bounds = bounds;
        }

        public int Width => _bounds.Width;
        public int Height => _bounds.Height;

        public void WriteAt(int x, int y, char c) => _parent.WriteAt(_bounds.X + x, _bounds.Y + y, c);
        public void WriteAt(int x, int y, string text) => _parent.WriteAt(_bounds.X + x, _bounds.Y + y, text);
        public void SetForeground(Color color) => _parent.SetForeground(color);
        public void SetBackground(Color color) => _parent.SetBackground(color);
        public void ResetColors() => _parent.ResetColors();
        public void SetDecoration(TextDecoration decoration) { }
        public void ApplyStyle(TextStyle style) { }
        public void Fill(int x, int y, int width, int height, char c = ' ') { }
        public void Clear() { }

        public IRenderContext CreateSubContext(Rect bounds) =>
            new SubContext(_parent, new Rect(_bounds.X + bounds.X, _bounds.Y + bounds.Y, bounds.Width, bounds.Height));
    }
}
