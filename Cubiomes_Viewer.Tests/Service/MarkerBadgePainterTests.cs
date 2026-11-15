using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Extensions;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Tests.Controls;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class MarkerBadgePainterTests
{
    private const int Precision = 6;
    private const double CanvasSide = 100;
    private static readonly Color _background = Color.FromRgb(0, 160, 255);
    private static readonly Color _fill = Color.FromRgb(0, 120, 0);
    private static readonly Color _glyph = Color.FromRgb(255, 255, 255);

    [Theory]
    [InlineData(22, 1, 22)]
    [InlineData(16.5, 1, 20)]
    [InlineData(33, 1, 42)]
    [InlineData(44, 1, 44)]
    [InlineData(55, 1, 64)]
    [InlineData(66, 1, 66)]
    [InlineData(22, 1.25, 22)]
    [InlineData(22, 1.5, 42)]
    [InlineData(22, 2, 44)]
    public void GetSide_OfAnySize_IsTheNearestSideAGlyphOnWholeDevicePixelsAllows(double side, double scaling, int devicePixels)
        => Assert.Equal(devicePixels, MarkerBadgePainter.GetSide(side, new PixelGrid(scaling, 0, 0)) * scaling, Precision);

    [Fact]
    public void GetSide_WithAMaximum_StaysWithinIt()
        => Assert.Equal(20, MarkerBadgePainter.GetSide(22, PixelGrid.Unscaled, maximumSide: 20), Precision);

    [AvaloniaTheory]
    [InlineData(16.5, 1)]
    [InlineData(22, 1)]
    [InlineData(33, 1)]
    [InlineData(66, 1)]
    [InlineData(22, 1.25)]
    public void Draw_AnySize_FramesTheGlyphWithFillAndKeepsItsCornersOnTheBadge(double side, double scaling)
    {
        var badge = new PaintedBadge(side, SquareGlyph.In(_glyph), new SolidColorBrush(_fill)) { Width = CanvasSide, Height = CanvasSide };
        var window = new Window
        {
            Width = CanvasSide * scaling,
            Height = CanvasSide * scaling,
            Background = new SolidColorBrush(_background),
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(scaling, scaling), Child = badge },
        };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var frame = Assert.IsType<WriteableBitmap>(window.CaptureRenderedFrame(), exactMatch: false);

            var (_, glyph) = FramePixels.Find(frame, ToRgb(_glyph));
            var (middleX, middleY) = (glyph.X + glyph.Width / 2, glyph.Y + glyph.Height / 2);

            Assert.All([(glyph.X - 1, middleY), (glyph.Right, middleY), (middleX, glyph.Y - 1), (middleX, glyph.Bottom)],
                beside => Assert.Equal(ToRgb(_fill), FramePixels.Read(frame, beside.Item1, beside.Item2)));
            Assert.All([(glyph.X - 1, glyph.Y - 1), (glyph.Right, glyph.Y - 1), (glyph.X - 1, glyph.Bottom), (glyph.Right, glyph.Bottom)],
                corner => Assert.NotEqual(ToRgb(_background), FramePixels.Read(frame, corner.Item1, corner.Item2)));
        }
        finally
        {
            window.Close();
        }
    }

    private static RgbColor ToRgb(Color color) => new(color.R, color.G, color.B);

    /// <summary>Draws a badge as the map does, with no box to keep within.</summary>
    private sealed class PaintedBadge(double side, IImage glyph, IBrush fill) : Control
    {
        public override void Render(DrawingContext context)
            => MarkerBadgePainter.Draw(context, fill, glyph, new Point(Bounds.Width / 2, Bounds.Height / 2), side, this.GetPixelGrid(), Brushes.Black);
    }
}
