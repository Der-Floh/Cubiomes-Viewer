using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>
/// Draws the badge of a marker with its edges, outline and glyph on whole device pixels, so that badges of one size look
/// alike wherever they land and however far the view is scaled.
/// </summary>
public static class MarkerBadgePainter
{
    private const double ReferenceSide = 22;
    private const double OutlineRatio = 1 / ReferenceSide;
    private const double CornerRatio = 5 / ReferenceSide;
    private const double GlyphRatio = 14 / ReferenceSide;

    /// <summary>Draws a badge of about <paramref name="side"/> units centred on a point.</summary>
    public static void Draw(DrawingContext context, IBrush fill, IImage glyph, Point center, double side, PixelGrid pixels, IBrush outline)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fill);
        ArgumentNullException.ThrowIfNull(glyph);
        ArgumentNullException.ThrowIfNull(outline);

        var badgeSide = pixels.RoundLength(side, minimumPixels: 1);
        var badge = new Rect(pixels.SnapX(center.X - badgeSide / 2), pixels.SnapY(center.Y - badgeSide / 2), badgeSide, badgeSide);
        var outlineWidth = pixels.RoundLength(side * OutlineRatio, minimumPixels: 1);
        var outerRadius = side * CornerRatio;
        var innerRadius = Math.Max(0, outerRadius - outlineWidth);

        context.DrawRectangle(outline, null, badge, outerRadius, outerRadius);
        context.DrawRectangle(fill, null, badge.Deflate(outlineWidth), innerRadius, innerRadius);
        context.DrawImage(glyph, badge.Deflate(pixels.RoundLength((badgeSide - side * GlyphRatio) / 2)));
    }
}
