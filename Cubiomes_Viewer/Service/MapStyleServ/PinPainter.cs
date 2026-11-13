using System.Xml.Linq;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>
/// Draws a pin from the shapes of the pin icons: the filled one as its face and outline, the outlined one as its rim, and
/// a glyph in the middle of its head. The tip lands on a device pixel, so pins of one size look alike wherever they land.
/// </summary>
public static class PinPainter
{
    private const string BodyIconPath = "/Assets/Icons/pin_filled.svg";
    private const string RimIconPath = "/Assets/Icons/pin.svg";
    private const string EvenOddFill = "F0 ";

    private const double IconSide = 24;
    private const double TipX = 12;
    private const double TipY = 22;
    private const double ShapeLeft = 4;
    private const double ShapeTop = 2;
    private const double HeadCenterY = 10.2;
    private const double GlyphSide = 10;
    private const double OutlineUnits = 0.75;

    private static readonly Uri _assetBaseUri = new($"avares://{typeof(PinPainter).Assembly.GetName().Name}/");
    private static readonly Lazy<Geometry> _body = new(() => LoadShape(BodyIconPath));
    private static readonly Lazy<Geometry> _rim = new(() => LoadShape(RimIconPath));

    /// <summary>Where the tip of a pin drawn to fill a square box lies.</summary>
    public static Point GetTip(Rect box) => new(box.X + box.Width * TipX / IconSide, box.Y + box.Height * TipY / IconSide);

    /// <summary>The area a pin covers when its icon square is <paramref name="size"/> units wide and its tip is on a point.</summary>
    public static Rect GetBounds(Point tip, double size)
    {
        var unit = size / IconSide;
        return new Rect(tip.X - (TipX - ShapeLeft) * unit, tip.Y - (TipY - ShapeTop) * unit, 2 * (TipX - ShapeLeft) * unit, (TipY - ShapeTop) * unit);
    }

    public static void Draw(DrawingContext context, IBrush fill, IImage? glyph, Point tip, double size, PixelGrid pixels, IBrush face, IBrush outline)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fill);
        ArgumentNullException.ThrowIfNull(face);
        ArgumentNullException.ThrowIfNull(outline);

        var unit = size / IconSide;
        var left = pixels.SnapX(tip.X) - TipX * unit;
        var top = pixels.SnapY(tip.Y) - TipY * unit;
        var outlinePen = new ImmutablePen(outline.ToImmutable(), pixels.RoundLength(OutlineUnits * unit, minimumPixels: 1) / unit);

        using (context.PushTransform(Matrix.CreateScale(unit, unit) * Matrix.CreateTranslation(left, top)))
        {
            context.DrawGeometry(face, null, _body.Value);
            context.DrawGeometry(fill, null, _rim.Value);
            context.DrawGeometry(null, outlinePen, _body.Value);
            if (glyph is not null)
                context.DrawImage(glyph, new Rect(TipX - GlyphSide / 2, HeadCenterY - GlyphSide / 2, GlyphSide, GlyphSide));
        }
    }

    private static Geometry LoadShape(string iconPath)
    {
        using var stream = AssetLoader.Open(new Uri(_assetBaseUri, iconPath));
        var data = XDocument.Load(stream).Descendants().FirstOrDefault(element => element.Name.LocalName == "path")?.Attribute("d")?.Value
            ?? throw new InvalidOperationException($"The icon '{iconPath}' has no path to draw pins with.");
        return Geometry.Parse(EvenOddFill + data);
    }
}
