using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>What a map shows: one layer of a world. Tiles of the same content can share a cache.</summary>
public readonly record struct MapContent(WorldKey World, LayerMode Layer, int Y, int Octaves = 0)
{
    /// <summary>
    /// Describes a layer as the user set it up. A height, an octave limit or structure rules the layer does not react
    /// to are left out, so that changing them does not generate the same tiles again. A view of the biome map is
    /// painted on the tiles of the biomes; the land biomes are those of the same world generated without its oceans.
    /// </summary>
    public static MapContent For(WorldKey world, LayerMode layer, int sampleHeight, int octaves) => new(
        (layer == LayerMode.LandBiomes ? world with { Flags = world.Flags | GeneratorFlags.NoBetaOcean } : world) with { Rules = StructureRules.Default },
        layer.ShowsBiomeMap ? LayerMode.Biomes : layer,
        layer.DependsOnHeight ? sampleHeight : 0,
        layer.ToClimateParameter() is null ? 0 : octaves);

    public TileRequest GetRequest(TileKey tile) => new(World, Layer, Y, tile, Octaves);
}
