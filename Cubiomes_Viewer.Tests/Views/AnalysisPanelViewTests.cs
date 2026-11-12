using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.ViewModels;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Views;

public sealed class AnalysisPanelViewTests : IDisposable
{
    private const int PanelWidth = 320;
    private const int PanelHeight = 2400;

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly AnalysisPanelViewModel _panel;
    private readonly Window _window;

    public AnalysisPanelViewTests()
    {
        _services.GetRequiredService<WorldSession>().Version = McVersion.V1_16_5;
        _panel = _services.GetRequiredService<AnalysisPanelViewModel>();
        var view = _services.GetRequiredService<AnalysisPanelView>();
        view.DataContext = _panel;
        _window = new Window { Width = PanelWidth, Height = PanelHeight, Content = view };
        _window.Show();
        HeadlessInput.Render();
    }

    public void Dispose()
    {
        _window.Close();
        _services.Dispose();
    }

    [AvaloniaFact]
    public void Tabs_EachShowItsOwnOptionsAndAction()
    {
        var headers = FindAll<TabItem>().Select(tab => tab.Header).OfType<AnalysisTabViewModel>().Select(tab => tab.Title).ToList();
        var firstAction = ShownActions();

        _panel.SelectedTabIndex = 1;
        HeadlessInput.Render();
        var secondAction = ShownActions();
        var structureToggles = FindAll<CheckBox>().Count(box => box.DataContext is MarkerToggleViewModel);

        _panel.SelectedTabIndex = 2;
        HeadlessInput.Render();

        Assert.Equal(["Biomes", "Structures", "Locations"], headers);
        Assert.Contains("Count biomes", firstAction);
        Assert.Contains("List structures", secondAction);
        Assert.True(structureToggles > 5);
        Assert.Contains("Find locations", ShownActions());
    }

    [AvaloniaFact]
    public async Task Results_AfterARun_AreListedAsLines()
    {
        _panel.Scope.Size = 512;

        await _panel.Tabs[0].RunCommand.ExecuteAsync(null);
        HeadlessInput.Render();

        var lines = FindAll<ListBoxItem>().Select(item => item.DataContext).OfType<AnalysisRow>().ToList();
        Assert.NotEmpty(lines);
        Assert.Equal(_panel.Tabs[0].Rows.Take(lines.Count), lines);
        Assert.Contains(FindAll<Button>(), button => Equals(button.Content, _panel.Tabs[0].SaveLabel));
    }

    private List<object?> ShownActions() => [.. FindAll<Button>().Where(button => button.Classes.Contains("accent")).Select(button => button.Content)];

    private IEnumerable<T> FindAll<T>() where T : Control
        => _window.GetVisualDescendants().OfType<T>().Where(control => control.IsEffectivelyVisible);
}
