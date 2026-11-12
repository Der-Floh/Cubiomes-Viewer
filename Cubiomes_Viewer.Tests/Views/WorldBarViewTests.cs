using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.ViewModels;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Views;

public sealed class WorldBarViewTests : IDisposable
{
    private const int BarWidth = 1200;
    private const int BarHeight = 120;

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly WorldBarViewModel _worldBar;
    private readonly Window _window;

    public WorldBarViewTests()
    {
        _worldBar = _services.GetRequiredService<WorldBarViewModel>();
        var view = new WorldBarView { DataContext = _worldBar };
        _window = new Window { Width = BarWidth, Height = BarHeight, Content = view };
        _window.Show();
        HeadlessInput.Render();
    }

    public void Dispose()
    {
        _window.Close();
        _services.Dispose();
    }

    [AvaloniaFact]
    public void RecentMenu_SeedClicked_OpensItAndTheButtonKeepsItsLabel()
    {
        Open("1");
        Open("2");
        var recent = _window.GetVisualDescendants().OfType<DropDownButton>().Single(button => Equals(button.Content, _worldBar.RecentSeedsLabel));

        var seeds = HeadlessInput.OpenMenu(recent);
        var shown = seeds.Select(item => item.Header).ToList();
        HeadlessInput.Click(seeds.Single(item => Equals(item.Header, "1")));
        var isOpen = recent.Flyout?.IsOpen;
        var reopened = HeadlessInput.OpenMenu(recent).Select(item => item.Header).ToList();

        Assert.Equal(["2", "1"], shown);
        Assert.Equal(1, _services.GetRequiredService<WorldSession>().Seed);
        Assert.Equal("1", _worldBar.SeedText);
        Assert.Equal(_worldBar.RecentSeedsLabel, recent.Content);
        Assert.False(isOpen);
        Assert.Equal(["1", "2"], reopened);
    }

    [AvaloniaFact]
    public void RecentMenu_NoSeedOpenedYet_IsDisabled()
    {
        var recent = _window.GetVisualDescendants().OfType<DropDownButton>().Single(button => Equals(button.Content, _worldBar.RecentSeedsLabel));

        Assert.False(recent.IsEffectivelyEnabled);
    }

    private void Open(string seed)
    {
        _worldBar.SeedText = seed;
        _worldBar.ApplySeedCommand.Execute(null);
        HeadlessInput.Render();
    }
}
