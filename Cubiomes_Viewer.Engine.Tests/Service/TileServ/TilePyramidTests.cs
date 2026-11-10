using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class TilePyramidTests
{
    private static readonly TileMetrics _twoPixelMetrics = new()
    {
        TileSamples = 128,
        Scales = [1, 4, 16, 64, 256, 1024, 4096],
        MinSamplePixels = 2,
        MaxBlocksPerPixel = 4096,
    };

    [Theory]
    [InlineData(0.25, 1)]
    [InlineData(0.5, 1)]
    [InlineData(1, 4)]
    [InlineData(2, 4)]
    [InlineData(4, 16)]
    [InlineData(100, 256)]
    [InlineData(4096, 4096)]
    public void SelectScale_TwoPixelSamples_PicksFinestLevelThatStaysTwoPixelsWide(double blocksPerPixel, int expectedScale)
        => Assert.Equal(expectedScale, TilePyramid.SelectScale(_twoPixelMetrics, blocksPerPixel));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 4)]
    [InlineData(64, 64)]
    [InlineData(512, 256)]
    public void SelectScale_LayeredMetrics_UsesOnePixelSamplesAndStopsAtCoarsestLevel(double blocksPerPixel, int expectedScale)
        => Assert.Equal(expectedScale, TilePyramid.SelectScale(TileMetrics.Layered, blocksPerPixel));

    [Fact]
    public void GetCoarserScale_FinestAndCoarsest_ReturnsNextOrNull()
    {
        Assert.Equal(4, TilePyramid.GetCoarserScale(TileMetrics.Noise, 1));
        Assert.Null(TilePyramid.GetCoarserScale(TileMetrics.Layered, 256));
    }

    [Fact]
    public void GetTileAt_NegativeBlock_FloorsTowardsNegativeInfinity()
    {
        var tile = TilePyramid.GetTileAt(TileMetrics.Noise, 4, -1, 512);

        Assert.Equal(new TileKey(4, -1, 1), tile);
    }

    [Fact]
    public void GetVisibleTiles_ViewAroundOrigin_CoversViewportNearestFirst()
    {
        var viewport = new MapViewport(new MapCamera(0, 0, 1), 512, 256);

        var tiles = TilePyramid.GetVisibleTiles(TileMetrics.Noise, 4, viewport);

        Assert.Equal(4, tiles.Count);
        Assert.All(tiles, tile => Assert.InRange(tile.X, -1, 0));
        Assert.All(tiles, tile => Assert.InRange(tile.Z, -1, 0));
    }

    [Fact]
    public void GetVisibleTiles_OffCentreView_ListsTileUnderTheCentreFirst()
    {
        var viewport = new MapViewport(new MapCamera(1000, -3000, 2), 1920, 1080);

        var tiles = TilePyramid.GetVisibleTiles(TileMetrics.Noise, 4, viewport);

        Assert.Equal(TilePyramid.GetTileAt(TileMetrics.Noise, 4, 1000, -3000), tiles[0]);
        Assert.Equal(tiles.Count, tiles.Distinct().Count());
    }

    [Fact]
    public void GetVisibleTiles_EmptyViewport_ReturnsNothing()
        => Assert.Empty(TilePyramid.GetVisibleTiles(TileMetrics.Noise, 4, new MapViewport(MapCamera.Default, 0, 0)));

    [Theory]
    [InlineData(1, 70, 70)]
    [InlineData(4, 70, 17)]
    [InlineData(16, -64, -16)]
    public void GetArea_BlockHeight_IsConvertedToBiomeCellsAboveScaleOne(int scale, int blockY, int expectedY)
    {
        var area = TilePyramid.GetArea(new TileKey(scale, -2, 3), 128, blockY);

        Assert.Equal(new BiomeArea(scale, -256, 384, 128, 128, expectedY), area);
    }

    [Theory]
    [InlineData(McVersion.V1_17_1, Dimension.Overworld, 256)]
    [InlineData(McVersion.V1_17_1, Dimension.Nether, 128)]
    [InlineData(McVersion.V1_18_2, Dimension.Overworld, 128)]
    [InlineData(McVersion.Beta1_7, Dimension.Overworld, 128)]
    public void For_World_PicksMetricsByGeneratorKind(McVersion version, Dimension dimension, int expectedTileSamples)
        => Assert.Equal(expectedTileSamples, TileMetrics.For(new WorldKey(0, version, dimension, GeneratorFlags.None)).TileSamples);
}
