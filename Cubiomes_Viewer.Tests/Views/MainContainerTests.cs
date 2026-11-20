using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

using Cubiomes_Viewer.Service.UiZoomServ;
using Cubiomes_Viewer.Service.WindowServ;
using Cubiomes_Viewer.ViewModels;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Views;

/// <summary>The single map view moving between the main window and a window of its own, as on the desktop.</summary>
public sealed class MainContainerTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;
    private readonly MapHostView _map;
    private readonly Window _window;

    public MainContainerTests()
    {
        var services = new ServiceCollection();
        App.ConfigureServices(services);
        TestServices.UseIsolatedPlatform(services);
        services.AddSingleton<IWindowService>(new DesktopWindows());
        _services = services.BuildServiceProvider();

        _main = _services.GetRequiredService<MainViewModel>();
        _window = new Window { Width = 1280, Height = 800, Content = _services.GetRequiredService<MainContainer>() };
        _window.Show();
        HeadlessInput.Render();
        _map = _window.GetVisualDescendants().OfType<MapHostView>().Single();
    }

    public void Dispose()
    {
        _window.Close();
        _services.Dispose();
    }

    [AvaloniaFact]
    public void Undocked_TheMapMovesIntoAWindowOfItsOwnAndBackAgain()
    {
        Assert.True(_main.CanUndockMap);
        Assert.Same(_window, TopLevel.GetTopLevel(_map));

        _main.ToggleMapWindowCommand.Execute(null);
        HeadlessInput.Render();
        var mapWindow = Assert.IsType<MapWindow>(TopLevel.GetTopLevel(_map));

        _main.ToggleMapWindowCommand.Execute(null);
        HeadlessInput.Render();

        Assert.Same(_window, TopLevel.GetTopLevel(_map));
        Assert.False(mapWindow.IsVisible);
        Assert.Equal("Open the map in its own window", _main.MapWindowLabel);
    }

    [AvaloniaFact]
    public void UiZoom_Doubled_ScalesTheShellsAndFallsBackToTheCompactOneWhenThePanelsNoLongerFit()
    {
        var zoom = _services.GetRequiredService<IUiZoomService>();
        var desktop = _window.GetVisualDescendants().OfType<DesktopShellView>().Single();
        var mobile = _window.GetVisualDescendants().OfType<MobileShellView>().Single();
        Assert.True(desktop.IsVisible);

        zoom.Apply(200);
        HeadlessInput.Render();

        var scale = Assert.IsType<ScaleTransform>(_window.GetVisualDescendants().OfType<LayoutTransformControl>().First().LayoutTransform);
        Assert.Equal((2.0, 2.0), (scale.ScaleX, scale.ScaleY));
        Assert.True(mobile.IsVisible);
        Assert.False(desktop.IsVisible);

        zoom.Apply(100);
        HeadlessInput.Render();

        Assert.True(desktop.IsVisible);
        Assert.False(mobile.IsVisible);
    }

    [AvaloniaFact]
    public void UiZoom_Raised_ScalesTheOpenListOfAComboBoxToo()
    {
        _services.GetRequiredService<IUiZoomService>().Apply(150);
        HeadlessInput.Render();
        var combo = _window.GetVisualDescendants().OfType<ComboBox>().First(box => box.IsEffectivelyVisible);

        combo.IsDropDownOpen = true;
        HeadlessInput.Render();

        var item = Assert.IsAssignableFrom<Control>(combo.ContainerFromIndex(0));
        Assert.Equal(1.5, item.TransformToVisual(_window)?.M11);
        combo.IsDropDownOpen = false;
    }

    [AvaloniaFact]
    public void Undocked_TheMapWindowShowsTheStatusStripOfTheMainWindow()
    {
        _main.ToggleMapWindowCommand.Execute(null);
        HeadlessInput.Render();
        var mapWindow = Assert.IsType<MapWindow>(TopLevel.GetTopLevel(_map));

        var strip = mapWindow.GetVisualDescendants().OfType<StatusBarView>().Single();

        Assert.Same(_main.StatusBar, strip.DataContext);
        Assert.Same(_main.StatusBar, _window.GetVisualDescendants().OfType<StatusBarView>().Single().DataContext);
    }

    [AvaloniaFact]
    public void MapWindow_ClosedByTheUser_BringsTheMapBack()
    {
        _main.ToggleMapWindowCommand.Execute(null);
        HeadlessInput.Render();
        var mapWindow = Assert.IsType<MapWindow>(TopLevel.GetTopLevel(_map));

        mapWindow.Close();
        HeadlessInput.Render();

        Assert.False(_main.IsMapUndocked);
        Assert.Same(_window, TopLevel.GetTopLevel(_map));
    }

    [AvaloniaFact]
    public void MainWindow_Closed_ClosesTheMapWindowToo()
    {
        _main.ToggleMapWindowCommand.Execute(null);
        HeadlessInput.Render();
        var mapWindow = Assert.IsType<MapWindow>(TopLevel.GetTopLevel(_map));

        _window.Close();

        Assert.False(mapWindow.IsVisible);
    }

    private sealed class DesktopWindows : IWindowService
    {
        public bool HasWindows => true;
    }
}
