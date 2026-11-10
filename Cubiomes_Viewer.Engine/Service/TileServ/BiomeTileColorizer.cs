using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public sealed class BiomeTileColorizer : ITileColorizer
{
    public const int BytesPerPixel = 4;

    private const byte Opaque = byte.MaxValue;

    public void Colorize(ReadOnlySpan<byte> biomes, BiomePalette palette, Span<byte> bgra)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (bgra.Length < biomes.Length * BytesPerPixel)
            throw new ArgumentException($"The pixel buffer holds {bgra.Length} bytes but {biomes.Length} samples need {biomes.Length * BytesPerPixel}.", nameof(bgra));

        for (var i = 0; i < biomes.Length; i++)
        {
            var pixel = bgra.Slice(i * BytesPerPixel, BytesPerPixel);
            if (biomes[i] == Constants.NoBiomeTileValue)
            {
                pixel.Clear();
                continue;
            }

            var color = palette[biomes[i]];
            pixel[0] = color.Blue;
            pixel[1] = color.Green;
            pixel[2] = color.Red;
            pixel[3] = Opaque;
        }
    }
}
