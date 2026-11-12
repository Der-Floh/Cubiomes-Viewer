using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

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
