using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Data.Export;

/// <summary>
/// How the pixels of a PNG are stored: an index into <see cref="Palette"/>, the entry at <see cref="TransparentIndex"/>
/// being transparent and small palettes packed several pixels to a byte, one byte of grey, or three bytes of red, green and
/// blue. Rows are handed over with one byte per pixel for palettes and greys.
/// </summary>
public sealed record PngFormat
{
    public const int MaxPaletteColors = 256;
    public const int FullBitDepth = 8;

    private static readonly int[] _packedBitDepths = [1, 2, 4];

    public static PngFormat Rgb { get; } = new() { ColorType = PngColorType.Rgb, TriesFilters = true };
    public static PngFormat Grayscale { get; } = new() { ColorType = PngColorType.Grayscale, TriesFilters = true };

    public PngColorType ColorType { get; private init; }
    public IReadOnlyList<RgbColor> Palette { get; private init; } = [];

    /// <summary>The palette entry that stands for no colour, or <c>-1</c>.</summary>
    public int TransparentIndex { get; private init; } = -1;

    /// <summary>The bits a pixel takes in the stored rows: fewer than 8 only for a packed palette.</summary>
    public int BitDepth { get; private init; } = FullBitDepth;

    /// <summary>
    /// Whether predicting the pixels from their neighbours with the PNG filters can pay off, which each band then tries: for
    /// greys, colours and palettes in ramp order, whose neighbouring pixels hold close values, and for packed palettes, whose
    /// bytes repeat from row to row. Full palettes of unrelated colours compress best unfiltered.
    /// </summary>
    public bool TriesFilters { get; private init; }

    /// <summary>The bytes a row hands over per pixel.</summary>
    public int BytesPerPixel => ColorType == PngColorType.Rgb ? 3 : 1;

    /// <summary>The bytes a stored row of an image this wide takes, packed where the palette is.</summary>
    public int GetRowBytes(int width) => ColorType == PngColorType.Rgb ? checked(width * 3) : checked((int)(((long)width * BitDepth + 7) / 8));

    /// <summary>
    /// A palette image. A packed one stores each pixel in as few bits as its palette needs, so it can index up to 16 colours;
    /// a smooth one has its palette in ramp order.
    /// </summary>
    public static PngFormat Indexed(IReadOnlyList<RgbColor> palette, int transparentIndex = -1, bool isSmooth = false, bool isPacked = false)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (palette.Count is 0 or > MaxPaletteColors)
            throw new ArgumentException($"A palette holds from 1 to {MaxPaletteColors} colours, not {palette.Count}.", nameof(palette));
        ArgumentOutOfRangeException.ThrowIfLessThan(transparentIndex, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(transparentIndex, palette.Count);

        var bitDepth = isPacked ? GetPackedBitDepth(palette.Count) : FullBitDepth;
        return new PngFormat { ColorType = PngColorType.Indexed, Palette = [.. palette], TransparentIndex = transparentIndex, BitDepth = bitDepth, TriesFilters = isSmooth || bitDepth < FullBitDepth };
    }

    /// <summary>The fewest bits of the packed depths PNG allows that index every colour, or 8 for a palette too large to pack.</summary>
    private static int GetPackedBitDepth(int colors)
        => _packedBitDepths.FirstOrDefault(bits => colors <= 1 << bits, FullBitDepth);
}
