using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
        Assert.Equal("Layers", view.Content);
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

        Assert.Equal("Search", Assert.Single(VisibleViews()).Content);
        Assert.Equal(2, _host.Children.Count);
    }

    [AvaloniaFact]
    public void Content_Null_HidesEveryView()
    {
        _host.Content = _layers;
        _host.Content = null;

        Assert.Empty(VisibleViews());
    }

    [AvaloniaFact]
    public void PrepareWhenIdle_Panels_BuildsTheirViewsHiddenOnceTheAppIsIdle()
    {
        _host.PrepareWhenIdle([_layers, _search]);
        Assert.Empty(_built);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal([_layers, _search], _built);
        Assert.Empty(VisibleViews());
        Assert.All(_host.Children, view => Assert.NotEmpty(view.GetVisualChildren()));
    }

    [AvaloniaFact]
    public void Content_APreparedPanel_ShowsItsViewWithoutBuildingItAgain()
    {
        _host.PrepareWhenIdle([_layers]);
        Dispatcher.UIThread.RunJobs();

        _host.Content = _layers;

        Assert.Equal("Layers", Assert.Single(VisibleViews()).Content);
        Assert.Equal([_layers], _built);
    }

    [AvaloniaFact]
    public void PrepareWhenIdle_APanelOpenAlready_BuildsOnlyTheOthers()
    {
        _host.Content = _layers;

        _host.PrepareWhenIdle([_layers, _search]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([_layers, _search], _built);
        Assert.Equal("Layers", Assert.Single(VisibleViews()).Content);
    }

    private Button Build(TestPanel panel)
    {
        _built.Add(panel);
        return new Button { Content = panel.Name };
    }

    private IEnumerable<Button> VisibleViews() => _host.Children.OfType<Button>().Where(view => view.IsVisible);

    private sealed record TestPanel(string Name);
}
