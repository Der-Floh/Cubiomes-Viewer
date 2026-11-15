using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Tests.Controls;

public sealed class PinIconTests
{
    private const double PinLeft = 2.7;
    private const double PinTop = 3.4;

    [AvaloniaTheory]
    [InlineData(32, 1, 16)]
    [InlineData(36, 1.25, 16)]
    [InlineData(64, 1, 32)]
    [InlineData(24, 1, 16)]
    public void Render_AtAnySize_DrawsTheGlyphAWholeNumberOfDevicePixelsPerGlyphPixel(double side, double interfaceScale, int glyphPixels)
    {
        var glyphColor = Color.FromRgb(255, 0, 0);
        var pin = new PinIcon
        {
            Fill = new SolidColorBrush(Color.FromRgb(0, 0, 200)),
            Face = Brushes.White,
            Outline = Brushes.Black,
            Glyph = SquareGlyph.In(glyphColor),
            Width = side,
            Height = side,
        };
        Canvas.SetLeft(pin, PinLeft);
        Canvas.SetTop(pin, PinTop);
        var window = new Window
        {
            Width = 100,
            Height = 100,
            Background = new SolidColorBrush(Color.FromRgb(0, 160, 0)),
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(interfaceScale, interfaceScale), Child = new Canvas { Children = { pin } } },
        };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var frame = Assert.IsType<WriteableBitmap>(window.CaptureRenderedFrame(), exactMatch: false);

            var (count, bounds) = FramePixels.Find(frame, new RgbColor(glyphColor.R, glyphColor.G, glyphColor.B));

            Assert.Equal((glyphPixels, glyphPixels), (bounds.Width, bounds.Height));
            Assert.Equal(glyphPixels * glyphPixels, count);
        }
        finally
        {
            window.Close();
        }
    }
}
