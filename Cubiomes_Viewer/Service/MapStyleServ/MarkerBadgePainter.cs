using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>
/// Draws the badge of a marker on whole device pixels. Its pixel art glyph takes a whole number of device pixels per glyph
/// pixel, its outline one such pixel and its padding two, of which it may give up one device pixel to come nearer the size
/// asked for. Badges of one size thus look alike wherever they land and however far the view is scaled.
/// </summary>
public static class MarkerBadgePainter
{
    private const int OutlinePixels = 1;
    private const int PaddingPixels = 2;

    /// <summary>How far the corners round per device pixel of outline and padding around the glyph; never far enough to reach it.</summary>
    private const double CornerPerFramePixel = 5.0 / 3;

    /// <summary>
    /// The side of a badge asked to be about <paramref name="side"/> units, kept within <paramref name="maximumSide"/> as long as
    /// the glyph fits at all.
    /// </summary>
    public static double GetSide(double side, PixelGrid pixels, double maximumSide = double.PositiveInfinity)
        => BadgeLayout.For(side, pixels, maximumSide).Side * pixels.PixelSize;

    /// <summary>
    /// Draws a badge of about <paramref name="side"/> units centred on a point, kept within <paramref name="maximumSide"/> as long
    /// as the glyph fits at all.
    /// </summary>
    public static void Draw(DrawingContext context, IBrush fill, IImage glyph, Point center, double side, PixelGrid pixels, IBrush outline,
        double maximumSide = double.PositiveInfinity)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fill);
        ArgumentNullException.ThrowIfNull(glyph);
        ArgumentNullException.ThrowIfNull(outline);

        var layout = BadgeLayout.For(side, pixels, maximumSide);
        var badgeSide = layout.Side * pixels.PixelSize;
        var badge = new Rect(pixels.SnapX(center.X - badgeSide / 2), pixels.SnapY(center.Y - badgeSide / 2), badgeSide, badgeSide);
        var outlineWidth = layout.Outline * pixels.PixelSize;
        var outerRadius = (layout.Outline + layout.Padding) * CornerPerFramePixel * pixels.PixelSize;
        var innerRadius = Math.Max(0, outerRadius - outlineWidth);

        context.DrawRectangle(outline, null, badge, outerRadius, outerRadius);
        context.DrawRectangle(fill, null, badge.Deflate(outlineWidth), innerRadius, innerRadius);
        context.DrawImage(glyph, badge.Deflate((layout.Outline + layout.Padding) * pixels.PixelSize));
    }

    /// <summary>A badge in device pixels: how many of them draw one pixel of the glyph, and how many lie between glyph and outline.</summary>
    private readonly record struct BadgeLayout(int Scale, int Padding)
    {
        public int Outline => Scale * OutlinePixels;

        public int Side => Bare(Scale) + 2 * Padding;

        public static BadgeLayout For(double side, PixelGrid pixels, double maximumSide)
        {
            var wanted = side * pixels.Scaling;
            var limit = maximumSide * pixels.Scaling;
            var scale = 1;
            while (Distance(wanted, scale + 1) < Distance(wanted, scale))
                scale++;
            while (scale > 1 && Smallest(scale) > limit)
                scale--;

            var padding = Math.Clamp((int)Math.Floor((wanted - Bare(scale)) / 2 + 0.5), LeastPadding(scale), MostPadding(scale));
            var room = (limit - Bare(scale)) / 2;
            return new BadgeLayout(scale, room < padding ? Math.Max(0, (int)Math.Floor(room)) : padding);
        }

        /// <summary>The side of a badge of this scale without padding.</summary>
        private static int Bare(int scale) => scale * (PixelGlyph.Pixels + 2 * OutlinePixels);

        private static int MostPadding(int scale) => scale * PaddingPixels;

        /// <summary>A badge gives up at most one device pixel of its padding; more would crowd its glyph.</summary>
        private static int LeastPadding(int scale) => MostPadding(scale) - 1;

        private static int Smallest(int scale) => Bare(scale) + 2 * LeastPadding(scale);

        private static int Largest(int scale) => Bare(scale) + 2 * MostPadding(scale);

        /// <summary>How far a side lies outside the sides a badge of this scale can take.</summary>
        private static double Distance(double wanted, int scale) => Math.Max(0, Math.Max(Smallest(scale) - wanted, wanted - Largest(scale)));
    }
}
