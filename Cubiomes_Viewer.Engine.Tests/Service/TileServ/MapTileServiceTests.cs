using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class MapTileServiceTests : IDisposable
{
    private const int MushroomFieldsBiomeId = 14;
    private const int SampleHeight = 63;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly CubiomesWorldGeneratorFactory _generatorFactory = new();
    private readonly InlineComputeService _computeService;
    private readonly MapTileService _tileService;

    public MapTileServiceTests()
    {
        var handlers = new JobHandlerRegistry([JobHandlerRegistration.Create<BiomeTileJob, TileData>(new BiomeTileJobHandler())]);
        _computeService = new InlineComputeService(handlers, _generatorFactory);
        _tileService = new MapTileService(_computeService, NullLogger<MapTileService>.Instance);
    }

    public void Dispose() => _computeService.Dispose();

    public static TheoryData<McVersion, Dimension, int> WorldsAndScales() => new()
    {
        { McVersion.V1_21_3, Dimension.Overworld, 1 },
        { McVersion.V1_21_3, Dimension.Overworld, 4 },
        { McVersion.V1_21_3, Dimension.Overworld, 64 },
        { McVersion.V1_21_3, Dimension.Overworld, 4096 },
        { McVersion.V1_21_3, Dimension.Nether, 16 },
        { McVersion.V1_21_3, Dimension.End, 4 },
        { McVersion.V1_17_1, Dimension.Overworld, 1 },
        { McVersion.V1_17_1, Dimension.Overworld, 256 },
        { McVersion.V1_12_2, Dimension.Overworld, 16 },
        { McVersion.Beta1_7, Dimension.Overworld, 4 },
    };

    [Theory]
    [MemberData(nameof(WorldsAndScales))]
    public async Task SetVisibleTiles_AnyWorldAndScale_ProducesTileMatchingPointSampling(McVersion version, Dimension dimension, int scale)
    {
        var world = new WorldKey(262, version, dimension, GeneratorFlags.None);
        var metrics = TileMetrics.For(world);
        var request = new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(scale, -1, 0));

        _tileService.SetVisibleTiles([request]);
        await WaitUntilIdleAsync(_tileService);

        Assert.True(_tileService.TryGetTile(request, out var tile));
        Assert.Equal(metrics.TileSamples * metrics.TileSamples, tile.Biomes.Length);

        using var generator = _generatorFactory.Create(version, GeneratorFlags.None);
        generator.Apply(dimension, world.Seed);
        var area = TilePyramid.GetArea(request.Tile, metrics.TileSamples, SampleHeight);
        foreach (var (sampleX, sampleZ) in ((int, int)[])[(0, 0), (17, 5), (metrics.TileSamples - 1, metrics.TileSamples - 1)])
        {
            var expected = generator.GetBiomeAt(scale, area.X + sampleX, area.Y, area.Z + sampleZ);
            Assert.Equal(expected < 0 ? byte.MaxValue : expected, tile.GetBiome(sampleX, sampleZ));
        }
    }

    [Fact]
    public async Task TryGetCachedBiome_AfterTileGenerated_ReturnsBiomeAtBlock()
    {
        var world = new WorldKey(262, McVersion.V1_18_2, Dimension.Overworld, GeneratorFlags.None);
        var request = new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(1, 0, 0));
        Assert.False(_tileService.TryGetCachedBiome(world, LayerMode.Biomes, SampleHeight, 0, 0, out _));

        _tileService.SetVisibleTiles([request]);
        await WaitUntilIdleAsync(_tileService);

        Assert.True(_tileService.TryGetCachedBiome(world, LayerMode.Biomes, SampleHeight, 0, 0, out var biome));
        Assert.Equal(MushroomFieldsBiomeId, biome);
    }

    [Fact]
    public async Task SetVisibleTiles_TileNoLongerVisible_CancelsItsGeneration()
    {
        var world = new WorldKey(1, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
        var dropped = new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(4, 0, 0));
        var kept = new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(4, 1, 0));

        _tileService.SetVisibleTiles([dropped]);
        _tileService.SetVisibleTiles([kept]);
        await WaitUntilIdleAsync(_tileService);

        Assert.False(_tileService.TryGetTile(dropped, out _));
        Assert.True(_tileService.TryGetTile(kept, out _));
    }

    [Fact]
    public async Task SetVisibleTiles_UnsupportedScaleForLayeredWorld_SkipsTheTileWithoutRetrying()
    {
        var world = new WorldKey(1, McVersion.V1_12_2, Dimension.Overworld, GeneratorFlags.None);
        var request = new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(1024, 0, 0));

        _tileService.SetVisibleTiles([request]);
        await WaitUntilIdleAsync(_tileService);
        _tileService.SetVisibleTiles([request]);

        Assert.Equal(0, _tileService.PendingCount);
        Assert.False(_tileService.TryGetTile(request, out _));
    }

    [Fact]
    public async Task SetVisibleTiles_WorkerFailsDuringTheJob_SkipsTheTileWithoutRetrying()
    {
        var computeService = new FailingComputeService();
        var tileService = new MapTileService(computeService, NullLogger<MapTileService>.Instance);
        var world = new WorldKey(1, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
        var request = new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(4, 0, 0));

        tileService.SetVisibleTiles([request]);
        await WaitUntilIdleAsync(tileService);
        tileService.SetVisibleTiles([request]);

        Assert.Equal(1, computeService.RunCount);
        Assert.Equal(0, tileService.PendingCount);
        Assert.False(tileService.TryGetTile(request, out _));
    }

    private static async Task WaitUntilIdleAsync(MapTileService tileService)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (tileService.PendingCount > 0)
        {
            Assert.True(DateTime.UtcNow < deadline, "Tiles were not generated in time.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    private sealed class FailingComputeService : IComputeService
    {
        public int WorkerCount => 1;
        public int RunCount { get; private set; }

        public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
        {
            RunCount++;
            return Task.FromException<TResult>(new ComputeJobException("The compute worker failed."));
        }
    }
}
