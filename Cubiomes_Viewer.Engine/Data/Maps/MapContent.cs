using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>What a map shows: one layer of a world. Tiles of the same content can share a cache.</summary>
public readonly record struct MapContent(WorldKey World, LayerMode Layer, int Y, int Octaves = 0)
{
    /// <summary>
    /// Describes a layer as the user set it up. A height or an octave limit the layer does not react to is left out,
    /// so that changing it does not generate the same tiles again.
    /// </summary>
    public static MapContent For(WorldKey world, LayerMode layer, int sampleHeight, int octaves)
        => new(world, layer, layer.DependsOnHeight ? sampleHeight : 0, layer.ToClimateParameter() is null ? 0 : octaves);

    public TileRequest GetRequest(TileKey tile) => new(World, Layer, Y, tile, Octaves);
}
