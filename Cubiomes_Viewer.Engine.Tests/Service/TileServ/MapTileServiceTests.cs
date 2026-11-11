using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
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
        var handlers = new JobHandlerRegistry([JobHandlerRegistration.Create<TileJob, TileData>(new TileJobHandler())]);
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
        var content = new MapContent(world, LayerMode.Biomes, SampleHeight);
        Assert.False(_tileService.TryGetCachedBiome(content, 0, 0, out _));

        _tileService.SetVisibleTiles([request]);
        await WaitUntilIdleAsync(_tileService);

        Assert.True(_tileService.TryGetCachedBiome(content, 0, 0, out var biome));
        Assert.Equal(MushroomFieldsBiomeId, biome);
        Assert.False(_tileService.TryGetCachedValue(content, 0, 0, out _));
    }

    public static TheoryData<McVersion, Dimension, LayerMode, int> LayersAndScales() => new()
    {
        { McVersion.V1_21_3, Dimension.Overworld, LayerMode.Temperature, 4 },
        { McVersion.V1_21_3, Dimension.Overworld, LayerMode.Depth, 64 },
        { McVersion.V1_21_3, Dimension.Overworld, LayerMode.Height, 16 },
        { McVersion.V1_21_3, Dimension.End, LayerMode.Height, 4 },
        { McVersion.V1_16_5, Dimension.Overworld, LayerMode.Height, 4 },
        { McVersion.Beta1_7, Dimension.Overworld, LayerMode.Humidity, 1 },
    };

    [Theory]
    [MemberData(nameof(LayersAndScales))]
    public async Task SetVisibleTiles_LayerOfNumbers_ProducesTheNumbersOfTheGenerator(McVersion version, Dimension dimension, LayerMode layer, int scale)
    {
        var world = new WorldKey(262, version, dimension, GeneratorFlags.None);
        var metrics = TileMetrics.For(world, layer);
        var content = new MapContent(world, layer, SampleHeight);
        var request = content.GetRequest(new TileKey(scale, -1, 0));

        _tileService.SetVisibleTiles([request]);
        await WaitUntilIdleAsync(_tileService);

        Assert.True(_tileService.TryGetTile(request, out var tile));
        Assert.True(tile.HasValues);
        Assert.Empty(tile.Biomes);
        Assert.Equal(GenerateValues(request, metrics.TileSamples), tile.Values);

        var tileBlocks = (int)TilePyramid.GetTileBlocks(metrics, scale);
        Assert.True(_tileService.TryGetCachedValue(content, -tileBlocks + 5 * scale, 3 * scale, out var value));
        Assert.Equal(tile.GetValue(5, 3), value);
        Assert.False(_tileService.TryGetCachedBiome(content, -tileBlocks, 0, out _));
    }

    [Theory]
    [InlineData(LayerMode.Rivers, 16)]
    [InlineData(LayerMode.OceanTemperature, 64)]
    public async Task SetVisibleTiles_InnerLayer_ProducesTheBiomesOfThatLayer(LayerMode layer, int scale)
    {
        var world = new WorldKey(262, McVersion.V1_16_5, Dimension.Overworld, GeneratorFlags.None);
        var metrics = TileMetrics.For(world, layer);
        var request = new MapContent(world, layer, 0).GetRequest(new TileKey(scale, 0, -1));

        _tileService.SetVisibleTiles([request]);
        await WaitUntilIdleAsync(_tileService);

        using var generator = _generatorFactory.Create(world.Version, GeneratorFlags.None);
        generator.Apply(world.Dimension, world.Seed);
        var expected = new byte[metrics.TileSamples * metrics.TileSamples];
        generator.GenerateLayer(Assert.IsType<GeneratorLayer>(layer.ToGeneratorLayer()), TilePyramid.GetArea(request.Tile, metrics.TileSamples, 0), expected);

        Assert.True(_tileService.TryGetTile(request, out var tile));
        Assert.False(tile.HasValues);
        Assert.Equal(expected, tile.Biomes);
    }

    [Fact]
    public async Task SetVisibleTiles_OctaveLimit_ProducesItsOwnTile()
    {
        var world = new WorldKey(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
        var full = new MapContent(world, LayerMode.Continentalness, 0).GetRequest(new TileKey(4, 0, 0));
        var widest = new MapContent(world, LayerMode.Continentalness, 0, Octaves: 1).GetRequest(new TileKey(4, 0, 0));

        _tileService.SetVisibleTiles([full, widest]);
        await WaitUntilIdleAsync(_tileService);

        Assert.True(_tileService.TryGetTile(full, out var fullTile));
        Assert.True(_tileService.TryGetTile(widest, out var widestTile));
        Assert.NotEqual(fullTile.Values, widestTile.Values);
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

    private short[] GenerateValues(TileRequest request, int samples)
    {
        using var generator = _generatorFactory.Create(request.World.Version, GeneratorFlags.None);
        generator.Apply(request.World.Dimension, request.World.Seed);

        var area = TilePyramid.GetArea(request.Tile, samples, request.Y);
        var values = new short[area.SampleCount];
        if (request.Layer.ToClimateParameter() is { } parameter)
            generator.GenerateClimate(parameter, request.Octaves, area, values);
        else
            generator.GenerateHeights(area, values);
        return values;
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
