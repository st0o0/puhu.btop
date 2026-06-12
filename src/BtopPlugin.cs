using Puhu.Plugin;

namespace Puhu.Btop;

public sealed class BtopPlugin : IPuhuPlugin
{
    public string Name => "Btop";

    public void Configure(IPuhuPluginBuilder builder)
    {
        builder.WithTab("btop", "/btop");
        // Routen, Services und Actors folgen in späteren Tasks.
    }
}
