using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class TileColorizerTests
{
    private const int PlainsBiomeId = 1;
    private const int ForestBiomeId = 4;
    private static readonly WorldKey _world = new(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);

    private readonly BiomeCatalog _biomeCatalog = new();
    private readonly LayerCatalog _layerCatalog = TestEngine.CreateLayerCatalog();
    private readonly TileColorizer _colorizer;

    public TileColorizerTests()
    {
        _colorizer = TestEngine.CreateTileColorizer();
    }

    [Fact]
    public void Colorize_KnownBiome_WritesOpaqueBgraPixel()
    {
        var expected = _biomeCatalog.DefaultPalette[PlainsBiomeId];
        var pixels = new byte[TileColorizer.BytesPerPixel];

        _colorizer.Colorize(CreateBiomeTile(PlainsBiomeId), default, pixels);

        Assert.Equal([expected.Blue, expected.Green, expected.Red, byte.MaxValue], pixels);
    }

    [Fact]
    public void Colorize_MissingBiome_WritesTransparentPixel()
    {
        var pixels = new byte[] { 1, 2, 3, 4 };

        _colorizer.Colorize(CreateBiomeTile(Constants.NoBiomeTileValue), default, pixels);

        Assert.Equal(new byte[TileColorizer.BytesPerPixel], pixels);
    }

    [Fact]
    public void Colorize_PixelBufferTooSmall_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => _colorizer.Colorize(CreateBiomeTile(PlainsBiomeId, PlainsBiomeId), default, new byte[TileColorizer.BytesPerPixel]));

    [Fact]
    public void Colorize_NumbersAtTheEndsOfTheRamp_TakeTheColoursOfItsEnds()
    {
        var ramp = GetRamp(LayerMode.Temperature);
        short[] values = [ToSteps(ramp.Minimum), ToSteps(ramp.Maximum), short.MinValue, short.MaxValue];
        var pixels = new byte[values.Length * TileColorizer.BytesPerPixel];

        _colorizer.Colorize(CreateValueTile(LayerMode.Temperature, values), default, pixels);

        Assert.Equal(ramp.Stops[0].Color, ReadColor(pixels, 0));
        Assert.Equal(ramp.Stops[^1].Color, ReadColor(pixels, 1));
        Assert.Equal(ramp.Stops[0].Color, ReadColor(pixels, 2));
        Assert.Equal(ramp.Stops[^1].Color, ReadColor(pixels, 3));
        Assert.All(Enumerable.Range(0, values.Length), index => Assert.Equal(byte.MaxValue, pixels[index * TileColorizer.BytesPerPixel + 3]));
    }

    [Theory]
    [InlineData(LayerMode.Temperature)]
    [InlineData(LayerMode.Continentalness)]
    [InlineData(LayerMode.Height)]
    public void Colorize_NumberAtAStopOfTheRamp_TakesTheStepOfAColourCloseToThatStop(LayerMode layer)
    {
        const int MaxChannelDifference = 4;
        var ramp = GetRamp(layer);
        var unit = GetValues(layer).Unit;
        var pixels = new byte[TileColorizer.BytesPerPixel];

        foreach (var stop in ramp.Stops)
        {
            _colorizer.Colorize(CreateValueTile(layer, (short)Math.Round(stop.Position * unit)), default, pixels);

            var color = ReadColor(pixels, 0);
            Assert.True(Math.Abs(color.Red - stop.Color.Red) <= MaxChannelDifference && Math.Abs(color.Green - stop.Color.Green) <= MaxChannelDifference
                && Math.Abs(color.Blue - stop.Color.Blue) <= MaxChannelDifference, $"{layer} at {stop.Position}: {color} instead of about {stop.Color}.");
        }
    }

    [Theory]
    [InlineData(LayerMode.Temperature, HeightStyle.Tinted, 256)]
    [InlineData(LayerMode.Height, HeightStyle.Tinted, 256)]
    [InlineData(LayerMode.Height, HeightStyle.Contours, 256)]
    public void GetPalette_LayerOfNumbers_HoldsEveryColourItPaints(LayerMode layer, HeightStyle height, int expectedColors)
    {
        var style = new TileStyle(height);
        var values = Enumerable.Range(0, 400).Select(index => (short)(index * 41 - 8000)).ToArray();
        var tile = CreateValueTile(layer, values);
        var pixels = new byte[values.Length * TileColorizer.BytesPerPixel];

        _colorizer.Colorize(tile, style, pixels);

        var palette = Assert.IsAssignableFrom<IReadOnlyList<RgbColor>>(_colorizer.GetPalette(tile.Request, style));
        Assert.Equal(expectedColors, palette.Count);
        Assert.All(Enumerable.Range(0, values.Length), sample => Assert.Contains(ReadColor(pixels, sample), palette));
    }

    [Fact]
    public void GetPalette_Contours_FollowsTheStepsWithTheirDarkerCopies()
    {
        var tile = CreateValueTile(LayerMode.Height, 0);

        var palette = _colorizer.GetPalette(tile.Request, new TileStyle(HeightStyle.Contours))!;

        var half = palette.Count / 2;
        Assert.All(Enumerable.Range(0, half), step => Assert.Equal(HeightRelief.DarkenForContour(palette[step]), palette[half + step]));
    }

    [Theory]
    [InlineData(LayerMode.Height, HeightStyle.Shaded)]
    [InlineData(LayerMode.Height, HeightStyle.Grayscale)]
    public void GetPalette_StyleWithMoreShadesThanAPalette_IsNone(LayerMode layer, HeightStyle height)
        => Assert.Null(_colorizer.GetPalette(CreateValueTile(layer, 0).Request, new TileStyle(height)));

    [Fact]
    public void GetPalette_Biomes_IsNone()
        => Assert.Null(_colorizer.GetPalette(CreateBiomeTile(PlainsBiomeId).Request, default));

    [Fact]
    public void Colorize_StructurePotential_DimsTheBiomesTheStructureCannotGenerateIn()
    {
        var palette = _biomeCatalog.DefaultPalette;
        var pixels = new byte[2 * TileColorizer.BytesPerPixel];

        _colorizer.Colorize(CreateBiomeTile(PlainsBiomeId, ForestBiomeId), new TileStyle(PotentialOf: StructureType.Village), pixels);

        var forest = ReadColor(pixels, 1);
        Assert.Equal(palette[PlainsBiomeId], ReadColor(pixels, 0));
        Assert.True(forest.Red == forest.Green && forest.Green == forest.Blue, $"The dimmed forest is {forest}.");
        Assert.True(forest.Green < palette[ForestBiomeId].Green / 2);
        Assert.Equal(byte.MaxValue, pixels[TileColorizer.BytesPerPixel + 3]);
    }

    [Fact]
    public void Colorize_PotentialOfAStructureWithoutABiomeRule_KeepsThePalette()
    {
        var palette = _biomeCatalog.DefaultPalette;
        var pixels = new byte[2 * TileColorizer.BytesPerPixel];

        _colorizer.Colorize(CreateBiomeTile(PlainsBiomeId, ForestBiomeId), new TileStyle(PotentialOf: StructureType.Geode), pixels);

        Assert.Equal([palette[PlainsBiomeId], palette[ForestBiomeId]], new[] { ReadColor(pixels, 0), ReadColor(pixels, 1) });
    }

    private ColorRamp GetRamp(LayerMode layer) => GetValues(layer).Ramp;

    private LayerValues GetValues(LayerMode layer)
        => Assert.IsType<LayerValues>(_layerCatalog.GetInfo(layer, _world.Version, _world.Dimension).Values);

    private short ToSteps(double value) => (short)Math.Round(value * GetValues(LayerMode.Temperature).Unit);

    private static TileData CreateBiomeTile(params byte[] biomes)
        => new() { Request = new TileRequest(_world, LayerMode.Biomes, 64, new TileKey(4, 0, 0)), Samples = biomes.Length, Biomes = biomes };

    /// <summary>A square tile of the numbers, row by row.</summary>
    private static TileData CreateValueTile(LayerMode layer, params short[] values)
        => new() { Request = new TileRequest(_world, layer, 0, new TileKey(4, 0, 0)), Samples = (int)Math.Sqrt(values.Length), Values = values };

    private static RgbColor ReadColor(byte[] pixels, int index)
    {
        var pixel = pixels.AsSpan(index * TileColorizer.BytesPerPixel, TileColorizer.BytesPerPixel);
        return new RgbColor(pixel[2], pixel[1], pixel[0]);
    }
}
