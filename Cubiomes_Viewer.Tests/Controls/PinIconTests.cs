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
    private const double WindowSide = 140;
    private static readonly Color _glyph = Color.FromRgb(255, 0, 0);
    private static readonly Color _rim = Color.FromRgb(0, 0, 200);

    [AvaloniaTheory]
    [InlineData(32, 1, 16)]
    [InlineData(36, 1.25, 16)]
    [InlineData(104, 1, 32)]
    [InlineData(24, 1, 16)]
    public void Render_AtAnySize_DrawsTheGlyphAWholeNumberOfDevicePixelsPerGlyphPixel(double side, double interfaceScale, int glyphPixels)
    {
        var frame = Render(side, interfaceScale);

        var (count, bounds) = FramePixels.Find(frame, ToRgb(_glyph));

        Assert.Equal((glyphPixels, glyphPixels), (bounds.Width, bounds.Height));
        Assert.Equal(glyphPixels * glyphPixels, count);
    }

    [AvaloniaTheory]
    [InlineData(52, 1)]
    [InlineData(78, 1)]
    [InlineData(104, 1)]
    [InlineData(52, 1.25)]
    public void Render_AtTheSizesOfMapPins_KeepsTheGlyphClearOfTheRim(double side, double interfaceScale)
    {
        var frame = Render(side, interfaceScale);

        var (_, glyph) = FramePixels.Find(frame, ToRgb(_glyph));
        var ring = Enumerable.Range(glyph.X - 1, glyph.Width + 2).SelectMany(x => new[] { (x, glyph.Y - 1), (x, glyph.Bottom) })
            .Concat(Enumerable.Range(glyph.Y, glyph.Height).SelectMany(y => new[] { (glyph.X - 1, y), (glyph.Right, y) }));

        Assert.All(ring, pixel => Assert.NotEqual(ToRgb(_rim), FramePixels.Read(frame, pixel.Item1, pixel.Item2)));
    }

    private static WriteableBitmap Render(double side, double interfaceScale)
    {
        var pin = new PinIcon
        {
            Fill = new SolidColorBrush(_rim),
            Face = Brushes.White,
            Outline = Brushes.Black,
            Glyph = SquareGlyph.In(_glyph),
            Width = side,
            Height = side,
        };
        Canvas.SetLeft(pin, PinLeft);
        Canvas.SetTop(pin, PinTop);
        var window = new Window
        {
            Width = WindowSide,
            Height = WindowSide,
            Background = new SolidColorBrush(Color.FromRgb(0, 160, 0)),
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(interfaceScale, interfaceScale), Child = new Canvas { Children = { pin } } },
        };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            return Assert.IsType<WriteableBitmap>(window.CaptureRenderedFrame(), exactMatch: false);
        }
        finally
        {
            window.Close();
        }
    }

    private static RgbColor ToRgb(Color color) => new(color.R, color.G, color.B);
}
