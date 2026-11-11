using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class TileColorizerHeightStyleTests
{
    private const int Samples = 6;
    private const int Scale = 4;
    private const int BlocksPerStep = 2;
    private const double ContourBrightness = 0.55;
    private static readonly WorldKey _world = new(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);

    private readonly TileColorizer _colorizer = new(new BiomeCatalog(), new LayerCatalog());

    [Fact]
    public void Colorize_ShadedFlatGround_KeepsTheTintedColours()
    {
        var tile = CreateHeightTile((_, _) => 80);

        Assert.Equal(Paint(tile, HeightStyle.Tinted), Paint(tile, HeightStyle.Shaded));
    }

    [Theory]
    [InlineData(BlocksPerStep, 0)]
    [InlineData(0, BlocksPerStep)]
    public void Colorize_ShadedSlopeFacingNorthWest_IsBrighterThanItsTint(int risePerSampleEast, int risePerSampleSouth)
    {
        var tile = CreateHeightTile((x, z) => 80 + x * risePerSampleEast + z * risePerSampleSouth);

        var (tinted, shaded) = (Paint(tile, HeightStyle.Tinted), Paint(tile, HeightStyle.Shaded));

        Assert.All(Enumerable.Range(0, Samples * Samples), sample => Assert.True(GetLight(shaded, sample) > GetLight(tinted, sample)));
    }

    [Theory]
    [InlineData(-BlocksPerStep, 0)]
    [InlineData(0, -BlocksPerStep)]
    public void Colorize_ShadedSlopeFacingSouthEast_IsDarkerThanItsTint(int risePerSampleEast, int risePerSampleSouth)
    {
        var tile = CreateHeightTile((x, z) => 120 + x * risePerSampleEast + z * risePerSampleSouth);

        var (tinted, shaded) = (Paint(tile, HeightStyle.Tinted), Paint(tile, HeightStyle.Shaded));

        Assert.All(Enumerable.Range(0, Samples * Samples), sample => Assert.True(GetLight(shaded, sample) < GetLight(tinted, sample)));
    }

    [Fact]
    public void Colorize_Contours_DarkenOnlyTheSamplesBeforeTheHeightEntersAnotherBand()
    {
        int[] row = [70, 78, 82, 90, 94, 95];
        var tile = CreateHeightTile((x, _) => row[x]);

        var (tinted, contours) = (Paint(tile, HeightStyle.Tinted), Paint(tile, HeightStyle.Contours));

        for (var sample = 0; sample < Samples * Samples; sample++)
        {
            var isBeforeTheLine = sample % Samples == 1;
            var expected = GetChannels(tinted, sample).Select(channel => isBeforeTheLine ? (byte)Math.Round(channel * ContourBrightness) : channel);
            Assert.Equal(expected, GetChannels(contours, sample));
        }
    }

    [Theory]
    [InlineData(4, 16)]
    [InlineData(16, 16)]
    [InlineData(64, 32)]
    [InlineData(256, 64)]
    [InlineData(4096, 64)]
    public void GetContourBlocks_CoarserScale_SpacesTheLinesFurtherApart(int scale, int expectedBlocks)
        => Assert.Equal(expectedBlocks, HeightRelief.GetContourBlocks(scale));

    [Fact]
    public void Colorize_Grayscale_PaintsHigherGroundBrighterAndWithoutColour()
    {
        int[] row = [-64, 0, 63, 100, 140, 320];
        var tile = CreateHeightTile((x, _) => row[x]);

        var pixels = Paint(tile, HeightStyle.Grayscale);

        var grays = Enumerable.Range(0, Samples).Select(sample => GetChannels(pixels, sample)).ToArray();
        Assert.All(grays, gray => Assert.True(gray[0] == gray[1] && gray[1] == gray[2]));
        Assert.Equal([0, 90, 200, 255], new[] { grays[0][0], grays[2][0], grays[4][0], grays[5][0] });
        Assert.Equal(grays.Select(gray => gray[0]).Order(), grays.Select(gray => gray[0]));
    }

    [Theory]
    [InlineData(HeightStyle.Shaded)]
    [InlineData(HeightStyle.Contours)]
    [InlineData(HeightStyle.Grayscale)]
    public void Colorize_HeightStyleOnAnotherLayer_ChangesNothing(HeightStyle style)
    {
        var values = Enumerable.Range(0, Samples * Samples).Select(index => (short)(index * 500 - 9000)).ToArray();
        var tile = new TileData { Request = new TileRequest(_world, LayerMode.Temperature, 0, new TileKey(Scale, 0, 0)), Samples = Samples, Values = values };

        Assert.Equal(Paint(tile, HeightStyle.Tinted), Paint(tile, style));
    }

    private byte[] Paint(TileData tile, HeightStyle style)
    {
        var pixels = new byte[tile.Samples * tile.Samples * TileColorizer.BytesPerPixel];
        _colorizer.Colorize(tile, new TileStyle(style), pixels);
        return pixels;
    }

    private static TileData CreateHeightTile(Func<int, int, int> getBlocks)
    {
        var heights = new short[Samples * Samples];
        for (var index = 0; index < heights.Length; index++)
            heights[index] = (short)(getBlocks(index % Samples, index / Samples) * Constants.HeightUnit);

        return new TileData { Request = new TileRequest(_world, LayerMode.Height, 0, new TileKey(Scale, 0, 0)), Samples = Samples, Values = heights };
    }

    private static byte[] GetChannels(byte[] pixels, int sample) => pixels.AsSpan(sample * TileColorizer.BytesPerPixel, 3).ToArray();

    private static int GetLight(byte[] pixels, int sample) => GetChannels(pixels, sample).Sum(channel => channel);
}
