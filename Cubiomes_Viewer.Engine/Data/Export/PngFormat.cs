using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Data.Export;

/// <summary>
/// How the pixels of a PNG are stored: one byte per pixel that picks a colour of <see cref="Palette"/>, the entry at
/// <see cref="TransparentIndex"/> being transparent, or three bytes of red, green and blue.
/// </summary>
public sealed record PngFormat
{
    public const int MaxPaletteColors = 256;

    public static PngFormat Rgb { get; } = new() { ColorType = PngColorType.Rgb };

    public PngColorType ColorType { get; private init; }
    public IReadOnlyList<RgbColor> Palette { get; private init; } = [];

    /// <summary>The palette entry that stands for no colour, or <c>-1</c>.</summary>
    public int TransparentIndex { get; private init; } = -1;

    public int BytesPerPixel => ColorType == PngColorType.Rgb ? 3 : 1;

    public static PngFormat Indexed(IReadOnlyList<RgbColor> palette, int transparentIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (palette.Count is 0 or > MaxPaletteColors)
            throw new ArgumentException($"A palette holds from 1 to {MaxPaletteColors} colours, not {palette.Count}.", nameof(palette));
        ArgumentOutOfRangeException.ThrowIfLessThan(transparentIndex, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(transparentIndex, palette.Count);

        return new PngFormat { ColorType = PngColorType.Indexed, Palette = [.. palette], TransparentIndex = transparentIndex };
    }
}
