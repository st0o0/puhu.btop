using R3;
using Termina.Reactive;

namespace Puhu.Plugin.MyPlugin.Pages;

public sealed class MyViewModel : ReactiveViewModel
{
    public ReactiveProperty<string> StatusText { get; } = new("Hello from MyPlugin!");

    public void RequestShutdown() => Shutdown();

    public override void Dispose()
    {
        StatusText.Dispose();
        base.Dispose();
    }
}
