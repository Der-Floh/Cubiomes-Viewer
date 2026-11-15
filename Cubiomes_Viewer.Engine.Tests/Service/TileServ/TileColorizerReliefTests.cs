using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class TileColorizerReliefTests
{
    private const int Samples = 8;
    private const byte Plains = 1;
    private const int BlocksPerStep = 2;
    private static readonly WorldKey _world = new(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
    private static readonly TileStyle _relief = new(BiomeRelief: true);
    private static readonly TileKey _tileAtOneToFour = new(4, 0, 0);

    private readonly TileColorizer _colorizer = TestEngine.CreateTileColorizer();

    [Fact]
    public void GetReliefTile_BiomeTileOfTheNoiseOverworld_IsTheHeightTileWithTheSameKey()
    {
        var tile = new TileRequest(_world, LayerMode.Biomes, 256, new TileKey(16, -2, 3));

        Assert.Equal(GetHeightRequest(new TileKey(16, -2, 3)), _colorizer.GetReliefTile(tile, _relief));
    }

    [Fact]
    public void GetReliefTile_ReliefSwitchedOff_IsNull()
        => Assert.Null(_colorizer.GetReliefTile(new TileRequest(_world, LayerMode.Biomes, 256, _tileAtOneToFour), default));

    [Theory]
    [InlineData(LayerMode.Temperature)]
    [InlineData(LayerMode.Rivers)]
    [InlineData(LayerMode.Height)]
    public void GetReliefTile_LayerOtherThanTheBiomeMap_IsNull(LayerMode layer)
        => Assert.Null(_colorizer.GetReliefTile(new TileRequest(_world, layer, 0, _tileAtOneToFour), _relief));

    [Theory]
    [InlineData(McVersion.V1_21_3, Dimension.Nether, 4)]
    [InlineData(McVersion.Beta1_7, Dimension.Overworld, 4)]
    [InlineData(McVersion.V1_8_9, Dimension.End, 4)]
    [InlineData(McVersion.V1_16_5, Dimension.Overworld, 256)]
    [InlineData(McVersion.V1_21_3, Dimension.End, 64)]
    public void GetReliefTile_WorldOrScaleWithoutHeights_IsNull(McVersion version, Dimension dimension, int scale)
    {
        var world = new WorldKey(262, version, dimension, GeneratorFlags.None);

        Assert.Null(_colorizer.GetReliefTile(new TileRequest(world, LayerMode.Biomes, 256, new TileKey(scale, 0, 0)), _relief));
    }

    [Fact]
    public void Colorize_FlatHeights_KeepTheBiomeColours()
    {
        var tile = CreateBiomeTile(_tileAtOneToFour);

        Assert.Equal(Paint(tile, default), Paint(tile, _relief, CreateHeightTile(_tileAtOneToFour, (_, _) => 80)));
    }

    [Fact]
    public void Colorize_ReliefWithoutItsHeightsYet_PaintsTheBiomeColours()
    {
        var tile = CreateBiomeTile(_tileAtOneToFour);

        Assert.Equal(Paint(tile, default), Paint(tile, _relief));
    }

    [Theory]
    [InlineData(BlocksPerStep, 0)]
    [InlineData(0, BlocksPerStep)]
    public void Colorize_SlopeFacingNorthWest_IsBrighterThanTheBiomeColour(int risePerSampleEast, int risePerSampleSouth)
    {
        var tile = CreateBiomeTile(_tileAtOneToFour);
        var heights = CreateHeightTile(_tileAtOneToFour, (x, z) => 80 + x * risePerSampleEast + z * risePerSampleSouth);

        var (flat, shaded) = (Paint(tile, default), Paint(tile, _relief, heights));

        Assert.All(Enumerable.Range(0, Samples * Samples), pixel => Assert.True(GetLight(shaded, pixel) > GetLight(flat, pixel)));
    }

    [Theory]
    [InlineData(-BlocksPerStep, 0)]
    [InlineData(0, -BlocksPerStep)]
    public void Colorize_SlopeFacingSouthEast_IsDarkerThanTheBiomeColour(int risePerSampleEast, int risePerSampleSouth)
    {
        var tile = CreateBiomeTile(_tileAtOneToFour);
        var heights = CreateHeightTile(_tileAtOneToFour, (x, z) => 120 + x * risePerSampleEast + z * risePerSampleSouth);

        var (flat, shaded) = (Paint(tile, default), Paint(tile, _relief, heights));

        Assert.All(Enumerable.Range(0, Samples * Samples), pixel => Assert.True(GetLight(shaded, pixel) < GetLight(flat, pixel)));
    }

    [Fact]
    public void Colorize_SampleWithoutBiome_StaysTransparent()
    {
        var tile = CreateBiomeTile(_tileAtOneToFour, (x, _) => x < 4 ? Constants.NoBiomeTileValue : Plains);

        var shaded = Paint(tile, _relief, CreateHeightTile(_tileAtOneToFour, (x, z) => 80 + x * BlocksPerStep + z));

        Assert.All(Enumerable.Range(0, Samples * Samples).Where(pixel => pixel % Samples < 4), pixel => Assert.Equal([0, 0, 0, 0], GetPixel(shaded, pixel)));
    }

    [Fact]
    public void Colorize_HeightSampleSpanningFourPixels_BlendsIntoItsNeighbours()
    {
        var tile = CreateBiomeTile(new TileKey(1, 1, 0));
        var heights = CreateHeightTile(_tileAtOneToFour, (x, _) => 64 + x * x * BlocksPerStep);

        var shaded = Paint(tile, _relief, heights);

        var row = Enumerable.Range(0, Samples).Select(pixel => GetLight(shaded, pixel)).ToArray();
        Assert.All(row.Zip(row.Skip(1)), pair => Assert.True(pair.Second < pair.First));
    }

    [Fact]
    public void Colorize_TwoBiomeTilesUnderOneHeightTile_MeetWithoutAJump()
    {
        var heights = CreateHeightTile(_tileAtOneToFour, (x, _) => 64 + x * x * BlocksPerStep);
        var (west, east) = (Paint(CreateBiomeTile(new TileKey(1, 0, 0)), _relief, heights), Paint(CreateBiomeTile(new TileKey(1, 1, 0)), _relief, heights));

        var row = Enumerable.Range(0, Samples).Select(pixel => GetLight(west, pixel)).Concat(Enumerable.Range(0, Samples).Select(pixel => GetLight(east, pixel))).ToArray();

        var steps = row.Zip(row.Skip(1), (left, right) => Math.Abs(right - left)).ToArray();
        var largestInside = steps.Where((_, index) => index != Samples - 1).Max();
        Assert.True(steps[Samples - 1] <= largestInside, $"The step between the tiles is {steps[Samples - 1]}, inside them at most {largestInside}.");
    }

    [Fact]
    public void Colorize_HeightsOfAnotherTile_Throws()
    {
        var tile = CreateBiomeTile(_tileAtOneToFour);

        Assert.Throws<ArgumentException>(() => Paint(tile, _relief, CreateHeightTile(new TileKey(4, 1, 0), (_, _) => 80)));
        Assert.Throws<ArgumentException>(() => Paint(tile, default, CreateHeightTile(_tileAtOneToFour, (_, _) => 80)));
    }

    private byte[] Paint(TileData tile, TileStyle style, TileData? heights = null)
    {
        var pixels = new byte[tile.Samples * tile.Samples * TileColorizer.BytesPerPixel];
        _colorizer.Colorize(tile, style, pixels, heights);
        return pixels;
    }

    private static TileData CreateBiomeTile(TileKey key, Func<int, int, byte>? getBiome = null) => new()
    {
        Request = new TileRequest(_world, LayerMode.Biomes, 256, key),
        Samples = Samples,
        Biomes = [.. Enumerable.Range(0, Samples * Samples).Select(index => getBiome?.Invoke(index % Samples, index / Samples) ?? Plains)],
    };

    private static TileData CreateHeightTile(TileKey key, Func<int, int, int> getBlocks)
    {
        var heights = new short[Samples * Samples];
        for (var index = 0; index < heights.Length; index++)
            heights[index] = (short)(getBlocks(index % Samples, index / Samples) * Constants.HeightUnit);

        return new TileData { Request = GetHeightRequest(key), Samples = Samples, Values = heights };
    }

    private static TileRequest GetHeightRequest(TileKey key) => MapContent.For(_world, LayerMode.Height, 0, 0).GetRequest(key);

    private static byte[] GetPixel(byte[] pixels, int pixel) => pixels.AsSpan(pixel * TileColorizer.BytesPerPixel, TileColorizer.BytesPerPixel).ToArray();

    private static int GetLight(byte[] pixels, int pixel) => GetPixel(pixels, pixel).Take(3).Sum(channel => channel);
}
