using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
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

    public bool TryGetCachedBiome(MapContent content, int blockX, int blockZ, out int biome)
    {
        biome = Constants.NoBiomeId;
        if (!TryFindSample(content, blockX, blockZ, out var sample) || sample.Tile.HasValues)
            return false;

        var value = sample.Tile.GetBiome(sample.X, sample.Z);
        biome = value == Constants.NoBiomeTileValue ? Constants.NoBiomeId : value;
        return true;
    }

    public bool TryGetCachedValue(MapContent content, int blockX, int blockZ, out short value)
    {
        value = 0;
        if (!TryFindSample(content, blockX, blockZ, out var sample) || !sample.Tile.HasValues)
            return false;

        value = sample.Tile.GetValue(sample.X, sample.Z);
        return true;
    }

    private bool TryFindSample(MapContent content, int blockX, int blockZ, out CachedSample sample)
    {
        var metrics = TileMetrics.For(content.World, content.Layer);
        foreach (var scale in metrics.Scales)
        {
            var key = TilePyramid.GetTileAt(metrics, scale, blockX, blockZ);
            if (!_cache.TryGet(content.GetRequest(key), out var tile))
                continue;

            var tileBlocks = TilePyramid.GetTileBlocks(metrics, scale);
            sample = new CachedSample(tile, (int)((blockX - key.X * tileBlocks) / scale), (int)((blockZ - key.Z * tileBlocks) / scale));
            return true;
        }

        sample = default;
        return false;
    }

    private static TileJob CreateJob(TileRequest request) => new(request, TileMetrics.For(request.World, request.Layer).TileSamples);

    private readonly record struct CachedSample(TileData Tile, int X, int Z);
}
