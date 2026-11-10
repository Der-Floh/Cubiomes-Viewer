using Cubiomes_Viewer.Engine.Data.Biomes;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public interface ITileColorizer
{
    /// <summary>
    /// Writes one opaque BGRA pixel (4 bytes) per biome sample. Samples without a biome become transparent.
    /// </summary>
    void Colorize(ReadOnlySpan<byte> biomes, BiomePalette palette, Span<byte> bgra);
}
