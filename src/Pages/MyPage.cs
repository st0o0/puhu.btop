using Puhu.Plugin;
using Termina.Layout;
using Termina.Reactive;
using Termina.Terminal;

namespace Puhu.Plugin.MyPlugin.Pages;

public sealed class MyPage : ReactivePage<MyViewModel>, IKeyHintProvider
{
    private readonly ITabNavigator _tabNavigator;

    public MyPage(ITabNavigator tabNavigator)
    {
        _tabNavigator = tabNavigator;
    }

    public string[] GetKeyHints() => ["Esc:Quit", "Tab:Switch"];

    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical(
            new TextNode(ViewModel.StatusText.Value)
                .WithForeground(Color.Cyan)
                .AlignCenter()
                .Fill());
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();

        KeyBindings.RegisterGlobalKeys(
            () => ViewModel.RequestShutdown(),
            path => Navigate(path),
            _tabNavigator);
    }
}
