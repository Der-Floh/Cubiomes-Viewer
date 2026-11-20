using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public interface ITileColorizer
{
    /// <summary>
    /// Writes one BGRA pixel (4 bytes) per sample of a tile: the colour of its biome, or the colour its layer gives
    /// its number in the chosen style. Samples without a biome become transparent. A tile of the biome map is shaded
    /// with <paramref name="heights"/>, which must be the tile <see cref="GetReliefTile"/> names for it.
    /// </summary>
    void Colorize(TileData tile, TileStyle style, Span<byte> bgra, TileData? heights = null);

    /// <summary>
    /// The height tile whose heights shade a tile of the biome map in a style, or <see langword="null"/> when the style
    /// leaves the biome map flat, the tile shows another layer, its world has no heights or no height tile covers it.
    /// </summary>
    TileRequest? GetReliefTile(TileRequest tile, TileStyle style);

    /// <summary>Whether <see cref="Colorize"/> paints every sample of tiles like this one in a style in a shade of grey.</summary>
    bool PaintsGray(TileRequest tile, TileStyle style);

    /// <summary>
    /// The at most 256 colours <see cref="Colorize"/> paints every sample of tiles like this one in a style from, in ramp
    /// order, or <see langword="null"/> when it has more colours than that or the tiles show biomes.
    /// </summary>
    IReadOnlyList<RgbColor>? GetPalette(TileRequest tile, TileStyle style);

    /// <summary>The colour <see cref="Colorize"/> gives each biome id in a style; the id that marks no biome has none that counts.</summary>
    IReadOnlyList<RgbColor> GetBiomeColors(TileStyle style, McVersion version);
}
