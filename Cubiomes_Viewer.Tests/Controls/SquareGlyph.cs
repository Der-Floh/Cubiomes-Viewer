using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace Cubiomes_Viewer.Tests.Controls;

/// <summary>A glyph that fills its whole 16 pixel square, so every device pixel it covers shows its colour unblended.</summary>
internal static class SquareGlyph
{
    private const string Svg = """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16" fill="currentColor"><rect width="16" height="16"/></svg>""";

    public static SvgImage In(Color color) => new() { Source = SvgSource.LoadFromSvg(Svg), CurrentColor = color };
}
