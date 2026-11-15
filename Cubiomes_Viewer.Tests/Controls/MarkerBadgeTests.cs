using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Tests.Controls;

public sealed class MarkerBadgeTests
{
    private const double InterfaceScale = 1.25;
    private const int BadgeSide = 20;
    private const int BadgeTop = 4;
    private static readonly Color _background = Color.FromRgb(0, 160, 255);

    [AvaloniaFact]
    public void Render_AtAFractionalInterfaceSize_DrawsEveryBadgeAlikeWithCrispEdges()
    {
        var marker = new MapStyleService(new BiomeCatalog(), new VersionCatalog()).GetStructureMarker(StructureType.Village);
        var fill = ((ISolidColorBrush)marker.Fill).Color;
        var canvas = new Canvas { Children = { CreateBadge(marker, 3.3), CreateBadge(marker, 30.9) } };
        var window = new Window
        {
            Width = 100,
            Height = 40,
            Background = new SolidColorBrush(_background),
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(InterfaceScale, InterfaceScale), Child = canvas },
        };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var frame = Assert.IsType<WriteableBitmap>(window.CaptureRenderedFrame(), exactMatch: false);

            var row = (int)((BadgeTop + BadgeSide / 2.0) * InterfaceScale);
            var edges = FindBadgeEdges(frame, row);
            Assert.Equal(2, edges.Count);
            Assert.All(edges, edge =>
            {
                Assert.True(IsDark(FramePixels.Read(frame, edge.Left, row)), "The outline is blurred into the background.");
                Assert.Equal(new RgbColor(fill.R, fill.G, fill.B), FramePixels.Read(frame, edge.Left + 1, row));
                Assert.Equal(new RgbColor(fill.R, fill.G, fill.B), FramePixels.Read(frame, edge.Right - 1, row));
                Assert.True(IsDark(FramePixels.Read(frame, edge.Right, row)), "The outline is blurred into the background.");
            });
            Assert.Equal(edges[0].Right - edges[0].Left, edges[1].Right - edges[1].Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(22, 1.25, 16)]
    [InlineData(44, 1, 32)]
    [InlineData(30, 1.5, 32)]
    public void Render_AtAnySize_DrawsTheGlyphAWholeNumberOfDevicePixelsPerGlyphPixel(double side, double interfaceScale, int glyphPixels)
    {
        var glyphColor = Color.FromRgb(255, 255, 255);
        var badge = new MarkerBadge { Fill = new SolidColorBrush(Color.FromRgb(0, 120, 0)), Glyph = SquareGlyph.In(glyphColor), Width = side, Height = side };
        Canvas.SetLeft(badge, 3.3);
        Canvas.SetTop(badge, BadgeTop);
        var window = new Window
        {
            Width = 100,
            Height = 100,
            Background = new SolidColorBrush(_background),
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(interfaceScale, interfaceScale), Child = new Canvas { Children = { badge } } },
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

    private static MarkerBadge CreateBadge(MapMarker marker, double left)
    {
        var badge = new MarkerBadge { Fill = marker.Fill, Glyph = marker.Glyph, Width = BadgeSide, Height = BadgeSide };
        Canvas.SetLeft(badge, left);
        Canvas.SetTop(badge, BadgeTop);
        return badge;
    }

    /// <summary>Returns the first and last pixel of each run of pixels along a row that are not the background.</summary>
    private static List<(int Left, int Right)> FindBadgeEdges(WriteableBitmap frame, int row)
    {
        List<(int Left, int Right)> edges = [];
        int? start = null;
        for (var x = 0; x < frame.PixelSize.Width; x++)
        {
            var isBadge = FramePixels.Read(frame, x, row) != new RgbColor(_background.R, _background.G, _background.B);
            if (isBadge && start is null)
                start = x;
            else if (!isBadge && start is { } left)
            {
                edges.Add((left, x - 1));
                start = null;
            }
        }

        return edges;
    }

    private static bool IsDark(RgbColor color) => color.Red < 100 && color.Green < 100 && color.Blue < 100;
}
