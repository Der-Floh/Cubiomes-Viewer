using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>
/// The glyphs of markers are pixel art 16 pixels square. They keep pixels of one size only when drawn a whole number of
/// device pixels per glyph pixel and on whole device pixels.
/// </summary>
public static class PixelGlyph
{
    public const int Pixels = 16;

    /// <summary>The side of a glyph drawn <paramref name="scale"/> device pixels per glyph pixel, in units of the control.</summary>
    public static double GetSide(int scale, PixelGrid pixels) => scale * Pixels * pixels.PixelSize;
}
