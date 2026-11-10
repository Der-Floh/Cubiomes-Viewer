using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public sealed class MapTileService : IMapTileService
{
    public const long DefaultCacheBudgetBytes = 256L * 1024 * 1024;

    public event Action? StateChanged;

    public int PendingCount
    {
        get
        {
            lock (_gate)
                return _pending.Count;
        }
    }

    public long CacheBudgetBytes
    {
        get => _cache.BudgetBytes;
        set => _cache.BudgetBytes = value;
    }

    private readonly IComputeService _computeService;
    private readonly ILogger<MapTileService> _logger;
    private readonly TileCache _cache = new(DefaultCacheBudgetBytes);
    private readonly Dictionary<TileRequest, CancellationTokenSource> _pending = [];
    private readonly HashSet<TileRequest> _failed = [];
    private readonly object _gate = new();

    public MapTileService(IComputeService computeService, ILogger<MapTileService> logger)
    {
        _computeService = computeService;
        _logger = logger;
    }

    public bool TryGetTile(TileRequest request, [NotNullWhen(true)] out TileData? tile) => _cache.TryGet(request, out tile);

    public void SetVisibleTiles(IReadOnlyList<TileRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        List<(TileRequest Request, int Order, CancellationTokenSource Cancellation)> started = [];
        lock (_gate)
        {
            var wanted = requests.ToHashSet();
            foreach (var (request, cancellation) in _pending.Where(pending => !wanted.Contains(pending.Key)).ToArray())
            {
                _pending.Remove(request);
                cancellation.Cancel();
            }

            for (var order = 0; order < requests.Count; order++)
            {
                var request = requests[order];
                if (_pending.ContainsKey(request) || _failed.Contains(request) || _cache.Contains(request))
                    continue;

                var cancellation = new CancellationTokenSource();
                _pending.Add(request, cancellation);
                started.Add((request, order, cancellation));
            }
        }

        foreach (var (request, order, cancellation) in started)
            _ = GenerateAsync(request, order, cancellation);

        StateChanged?.Invoke();
    }

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

    private async Task GenerateAsync(TileRequest request, int order, CancellationTokenSource cancellation)
    {
        try
        {
            var job = new BiomeTileJob(request, TileMetrics.For(request.World).TileSamples);
            var tile = await _computeService.RunAsync(job, new JobPriority(JobPriorityClass.Visible, order), cancellation.Token).ConfigureAwait(false);
            _cache.Add(tile);
        }
        catch (OperationCanceledException)
        {
            // The tile left the visible set before it was generated.
        }
        catch (CubiomesNativeException ex)
        {
            _logger.LogWarning(ex, "Tile {Tile} could not be generated and is skipped", request.Tile);
            lock (_gate)
                _failed.Add(request);
        }
        finally
        {
            lock (_gate)
            {
                if (_pending.TryGetValue(request, out var current) && current == cancellation)
                    _pending.Remove(request);
            }

            cancellation.Dispose();
            StateChanged?.Invoke();
        }
    }
}
