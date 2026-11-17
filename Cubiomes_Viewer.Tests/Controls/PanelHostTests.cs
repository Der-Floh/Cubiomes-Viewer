using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Controls;

namespace Cubiomes_Viewer.Tests.Controls;

public sealed class PanelHostTests : IDisposable
{
    private readonly PanelHost _host = new();
    private readonly Window _window;
    private readonly List<TestPanel> _built = [];
    private readonly TestPanel _layers = new("Layers");
    private readonly TestPanel _search = new("Search");

    public PanelHostTests()
    {
        _window = new Window { Content = _host };
        _window.DataTemplates.Add(new FuncDataTemplate<TestPanel>((panel, _) => Build(panel)));
        _window.Show();
    }

    public void Dispose() => _window.Close();

    [AvaloniaFact]
    public void Content_APanel_ShowsAViewBoundToIt()
    {
        _host.Content = _layers;

        var view = Assert.Single(VisibleViews());
        Assert.Same(_layers, view.DataContext);
        Assert.Equal("Layers", view.Text);
    }

    [AvaloniaFact]
    public void Content_APanelOpenedBefore_ShowsItsViewAgainWithoutBuildingIt()
    {
        _host.Content = _layers;
        var first = Assert.Single(VisibleViews());

        _host.Content = null;
        _host.Content = _layers;

        Assert.Same(first, Assert.Single(VisibleViews()));
        Assert.Equal([_layers], _built);
    }

    [AvaloniaFact]
    public void Content_AnotherPanel_HidesTheViewOfTheFirst()
    {
        _host.Content = _layers;
        _host.Content = _search;

        Assert.Equal("Search", Assert.Single(VisibleViews()).Text);
        Assert.Equal(2, _host.Children.Count);
    }

    [AvaloniaFact]
    public void Content_Null_HidesEveryView()
    {
        _host.Content = _layers;
        _host.Content = null;

        Assert.Empty(VisibleViews());
    }

    private TextBlock Build(TestPanel panel)
    {
        _built.Add(panel);
        return new TextBlock { Text = panel.Name };
    }

    private IEnumerable<TextBlock> VisibleViews() => _host.Children.OfType<TextBlock>().Where(view => view.IsVisible);

    private sealed record TestPanel(string Name);
}
