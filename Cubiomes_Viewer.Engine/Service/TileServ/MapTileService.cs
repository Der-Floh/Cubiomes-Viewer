using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public sealed class MapTileService : IMapTileService
{
    public const long DefaultCacheBudgetBytes = 256L * 1024 * 1024;

    public event Action? StateChanged
    {
        add => _loader.StateChanged += value;
        remove => _loader.StateChanged -= value;
    }

    public int PendingCount => _loader.PendingCount;

    public long CacheBudgetBytes
    {
        get => _cache.Budget;
        set => _cache.Budget = value;
    }

    private readonly LruCache<TileRequest, TileData> _cache = new(DefaultCacheBudgetBytes, static tile => tile.ByteSize);
    private readonly VisibleSetLoader<TileRequest, TileData> _loader;

    public MapTileService(IComputeService computeService, ILogger<MapTileService> logger)
    {
        _loader = new VisibleSetLoader<TileRequest, TileData>(computeService, _cache, CreateJob, JobPriorityClass.Visible, logger);
    }

    public bool TryGetTile(TileRequest request, [NotNullWhen(true)] out TileData? tile) => _loader.TryGet(request, out tile);

    public void SetVisibleTiles(IReadOnlyList<TileRequest> requests) => _loader.SetVisible(requests);

    public bool TryGetCachedBiome(WorldKey world, LayerMode layer, int y, int blockX, int blockZ, out int biome)
    {
        var metrics = TileMetrics.For(world);
        foreach (var scale in metrics.Scales)
        {
            var key = TilePyramid.GetTileAt(metrics, scale, blockX, blockZ);
            if (!_cache.TryGet(new TileRequest(world, layer, y, key), out var tile))
                continue;

            var tileBlocks = TilePyramid.GetTileBlocks(metrics, scale);
            var sampleX = (int)((blockX - key.X * tileBlocks) / scale);
            var sampleZ = (int)((blockZ - key.Z * tileBlocks) / scale);
            var value = tile.GetBiome(sampleX, sampleZ);
            biome = value == Constants.NoBiomeTileValue ? Constants.NoBiomeId : value;
            return true;
        }

        biome = Constants.NoBiomeId;
        return false;
    }

    private static BiomeTileJob CreateJob(TileRequest request) => new(request, TileMetrics.For(request.World).TileSamples);
}
