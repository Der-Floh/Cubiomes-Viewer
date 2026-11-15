using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>
/// Draws the badge of a marker on whole device pixels. Its pixel art glyph takes a whole number of device pixels per glyph
/// pixel and its outline one such pixel; the padding between them brings the badge as near the size asked for as it gets.
/// Badges of one size thus look alike wherever they land and however far the view is scaled.
/// </summary>
public static class MarkerBadgePainter
{
    private const int OutlinePixels = 1;
    private const int MostPaddingPixels = 2;
    private const double CornerRatio = 5.0 / 22;

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
        var outerRadius = badgeSide * CornerRatio;
        var innerRadius = Math.Max(0, outerRadius - outlineWidth);

        context.DrawRectangle(outline, null, badge, outerRadius, outerRadius);
        context.DrawRectangle(fill, null, badge.Deflate(outlineWidth), innerRadius, innerRadius);
        context.DrawImage(glyph, badge.Deflate((layout.Outline + layout.Padding) * pixels.PixelSize));
    }

    /// <summary>A badge in device pixels: how many of them draw one pixel of the glyph, and how many lie between glyph and outline.</summary>
    private readonly record struct BadgeLayout(int Scale, int Padding)
    {
        public int Outline => Scale * OutlinePixels;

        public int Side => Smallest(Scale) + 2 * Padding;

        public static BadgeLayout For(double side, PixelGrid pixels, double maximumSide)
        {
            var wanted = side * pixels.Scaling;
            var limit = maximumSide * pixels.Scaling;
            var scale = 1;
            while (Distance(wanted, scale + 1) < Distance(wanted, scale))
                scale++;
            while (scale > 1 && Smallest(scale) > limit)
                scale--;

            var padding = Math.Clamp((int)Math.Floor((wanted - Smallest(scale)) / 2 + 0.5), 0, MostPaddingPixels * scale);
            var room = (limit - Smallest(scale)) / 2;
            return new BadgeLayout(scale, room < padding ? Math.Max(0, (int)Math.Floor(room)) : padding);
        }

        /// <summary>The side of a badge of this scale without padding.</summary>
        private static int Smallest(int scale) => scale * (PixelGlyph.Pixels + 2 * OutlinePixels);

        private static int Largest(int scale) => Smallest(scale) + 2 * MostPaddingPixels * scale;

        /// <summary>How far a side lies outside the sides a badge of this scale can take.</summary>
        private static double Distance(double wanted, int scale) => Math.Max(0, Math.Max(Smallest(scale) - wanted, wanted - Largest(scale)));
    }
}
