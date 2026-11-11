using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// Everything that determines the content of a tile; used as the cache key. <see cref="Octaves"/> above zero limits a
/// climate layer to that many of its widest noise octaves.
/// </summary>
public readonly record struct TileRequest(WorldKey World, LayerMode Layer, int Y, TileKey Tile, int Octaves = 0);
