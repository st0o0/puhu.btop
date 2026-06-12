using Puhu.Plugin;

namespace Puhu.Plugin.MyPlugin;

public sealed class MyPlugin : IPuhuPlugin
{
    public string Name => "MyPlugin";

    public void Configure(IPuhuPluginBuilder builder)
    {
        builder
            .WithTab("MyPlugin", "/myplugin")
            .WithRoutes(termina =>
                termina.RegisterRoute<Pages.MyPage, Pages.MyViewModel>("/myplugin"));
    }
}
