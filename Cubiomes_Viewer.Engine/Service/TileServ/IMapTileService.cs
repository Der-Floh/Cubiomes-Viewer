using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>Generates the tiles a map view needs, keeps them in a cache and drops work that is no longer visible.</summary>
public interface IMapTileService
{
    /// <summary>Raised when a tile became available or the number of pending tiles changed. May fire on a worker thread.</summary>
    event Action? StateChanged;

    int PendingCount { get; }
    long CacheBudgetBytes { get; set; }

    bool TryGetTile(TileRequest request, [NotNullWhen(true)] out TileData? tile);

    /// <summary>
    /// Declares the tiles currently needed, most important first. Missing tiles are generated in that order;
    /// pending tiles that are no longer listed are cancelled.
    /// </summary>
    void SetVisibleTiles(IReadOnlyList<TileRequest> requests);

    /// <summary>Looks up the biome at a block position in the finest tile that is already cached.</summary>
    bool TryGetCachedBiome(WorldKey world, LayerMode layer, int y, int blockX, int blockZ, out int biome);
}
