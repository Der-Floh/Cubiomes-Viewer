using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public interface ITileColorizer
{
    /// <summary>
    /// Writes one BGRA pixel (4 bytes) per sample of a tile: the colour of its biome, or the colour its layer gives
    /// its number in the chosen style. Samples without a biome become transparent.
    /// </summary>
    void Colorize(TileData tile, TileStyle style, Span<byte> bgra);
}
