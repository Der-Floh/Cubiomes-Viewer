using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Tests.Controls;

public sealed class SvgIconTests
{
    private const int IconSize = 48;
    private const int ViewSize = IconSize * 2;
    private const string IconFolder = "/Assets/Icons/";
    private const double IconGrid = 24;
    private const double HomeWallX = 5;
    private const double HomeWallY = 15;
    private const double HomeInsideX = 7.5;
    private const double HomeInsideY = 16;

    [AvaloniaFact]
    public void Files_OfTheInterfaceIcons_FillWithTheForegroundColour()
    {
        var icons = AssetLoader.GetAssets(new Uri($"avares://Cubiomes_Viewer{IconFolder}"), null).Where(IsInterfaceIcon).ToList();

        Assert.NotEmpty(icons);
        Assert.All(icons, icon =>
        {
            using var reader = new StreamReader(AssetLoader.Open(icon));
            var svg = reader.ReadToEnd();
            Assert.Contains("fill=\"currentColor\"", svg, StringComparison.Ordinal);
            Assert.DoesNotContain("fill=\"#", svg, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public void Render_InAButton_DrawsTheFileInTheFirstFrameInTheButtonsForeground()
    {
        var button = new Button
        {
            Content = new SvgIcon { Source = new SvgIconSource { Outlined = "/Assets/Icons/add.svg" }, Width = IconSize, Height = IconSize },
            Foreground = Brushes.Red,
            Background = Brushes.White,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var window = new Window { Width = ViewSize, Height = ViewSize, Background = Brushes.White, Content = button };
        window.Show();

        var first = RenderCentre(window);
        button.Foreground = Brushes.Blue;
        var recoloured = RenderCentre(window);
        window.Close();

        Assert.Equal(new RgbColor(255, 0, 0), first);
        Assert.Equal(new RgbColor(0, 0, 255), recoloured);
    }

    /// <summary>The icons of buttons lie in the icon folder itself; the glyphs of markers and dimensions lie in folders below it.</summary>
    private static bool IsInterfaceIcon(Uri asset)
        => asset.AbsolutePath.EndsWith(".svg", StringComparison.Ordinal) && !asset.AbsolutePath[IconFolder.Length..].Contains('/', StringComparison.Ordinal);

    [AvaloniaFact]
    public void IsFilled_InAToggleButton_ShowsTheFilledFormWhileChecked()
    {
        var icon = new SvgIcon { Source = new SvgIconSource { Outlined = "/Assets/Icons/home.svg", Filled = "/Assets/Icons/home_filled.svg" }, Width = IconSize, Height = IconSize };
        var toggle = new ToggleButton { Content = icon, Foreground = Brushes.Red, Background = Brushes.White, Padding = new Thickness(0), BorderThickness = new Thickness(0) };
        var window = new Window { Width = ViewSize, Height = ViewSize, Background = Brushes.White, Content = toggle };
        window.Show();
        try
        {
            var (wall, inside) = RenderWallAndInside(window, icon);
            Assert.NotEqual(wall, inside);

            toggle.IsChecked = true;
            (wall, inside) = RenderWallAndInside(window, icon);

            Assert.True(icon.IsFilled);
            Assert.Equal(wall, inside);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IsFilled_InTheRail_ShowsTheFilledFormOfTheOpenPanelOnly()
    {
        var layers = new SvgIcon { Source = new SvgIconSource { Outlined = "/Assets/Icons/layers.svg", Filled = "/Assets/Icons/layers_filled.svg" } };
        var settings = new SvgIcon { Source = new SvgIconSource { Outlined = "/Assets/Icons/settings.svg", Filled = "/Assets/Icons/settings_filled.svg" } };
        var rail = new ListBox { Classes = { "rail" }, Items = { new ListBoxItem { Content = layers }, new ListBoxItem { Content = settings } } };
        var window = new Window { Content = rail };
        window.Show();
        try
        {
            rail.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.False(layers.IsFilled);
            Assert.True(settings.IsFilled);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Reads the home icon on a wall, which both forms fill, and inside the house, which only the filled form fills.</summary>
    private static (RgbColor Wall, RgbColor Inside) RenderWallAndInside(Window window, SvgIcon icon)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        return (Read(HomeWallX, HomeWallY), Read(HomeInsideX, HomeInsideY));

        RgbColor Read(double unitX, double unitY)
        {
            var point = icon.TranslatePoint(new Point(unitX * IconSize / IconGrid, unitY * IconSize / IconGrid), window) ?? default;
            return FramePixels.Read(frame, (int)point.X, (int)point.Y);
        }
    }

    private static RgbColor RenderCentre(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        return FramePixels.Read(frame, ViewSize / 2, ViewSize / 2);
    }
}
