using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Tests.Controls;

public sealed class SvgIconTests
{
    private const int IconSize = 48;
    private const int ViewSize = IconSize * 2;

    [AvaloniaFact]
    public void Render_InAButton_DrawsTheFileInTheFirstFrameInTheButtonsForeground()
    {
        var button = new Button
        {
            Content = new SvgIcon { Path = "/Assets/Icons/add.svg", Width = IconSize, Height = IconSize },
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

    private static RgbColor RenderCentre(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        return FramePixels.Read(frame, ViewSize / 2, ViewSize / 2);
    }
}
