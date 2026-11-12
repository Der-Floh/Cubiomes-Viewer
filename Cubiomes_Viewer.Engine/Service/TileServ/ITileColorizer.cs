using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public interface ITileColorizer
{
    /// <summary>
    /// Writes one BGRA pixel (4 bytes) per sample of a tile: the colour of its biome, or the colour its layer gives
    /// its number in the chosen style. Samples without a biome become transparent.
    /// </summary>
    void Colorize(TileData tile, TileStyle style, Span<byte> bgra);

    /// <summary>The colour <see cref="Colorize"/> gives each biome id in a style; the id that marks no biome has none that counts.</summary>
    IReadOnlyList<RgbColor> GetBiomeColors(TileStyle style, McVersion version);
}
