using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

public sealed class CubiomesWorldGeneratorLayerTests
{
    private const long Seed = 262;
    private const long SeedWithWarmOceans = -7379792620528906219;
    private const int Side = 256;
    private const int CellScale = 4;
    private const int SurfaceY = 16;
    private const int AllOctaves = 64;
    private const int SeaLevel = 63;
    private const byte River = 7;
    private const byte FrozenRiver = 11;
    private const byte WarmOcean = 44;
    private const byte DeepOcean = 24;
    private const byte Plains = 1;
    private const byte NoBiome = Constants.NoBiomeTileValue;
    private const int MushroomFieldsMaxContinentalness = -10500;

    private static readonly BiomeArea _area = new(CellScale, -Side / 2, -Side / 2, Side, Side, SurfaceY);
    private static readonly BiomeArea _coarseArea = new(16, -Side / 2, -Side / 2, Side, Side, SurfaceY);

    [Fact]
    public void GenerateClimate_MushroomFieldsAtTheOrigin_HaveTheContinentalnessTheyRequire()
    {
        using var generator = CreateGenerator(McVersion.V1_18_2, Dimension.Overworld);
        var origin = new BiomeArea(CellScale, 0, 0, 1, 1, SurfaceY);
        var value = new short[1];

        generator.GenerateClimate(ClimateParameter.Continentalness, 0, origin, value);

        Assert.True(value[0] < MushroomFieldsMaxContinentalness, $"Continentalness at the origin is {value[0]}.");
    }

    [Theory]
    [InlineData(ClimateParameter.Temperature)]
    [InlineData(ClimateParameter.Humidity)]
    [InlineData(ClimateParameter.Continentalness)]
    [InlineData(ClimateParameter.Erosion)]
    [InlineData(ClimateParameter.Depth)]
    [InlineData(ClimateParameter.Weirdness)]
    public void GenerateClimate_NoiseBasedOverworld_VariesWithinTwoUnitsAroundZero(ClimateParameter parameter)
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var values = new short[_area.SampleCount];

        generator.GenerateClimate(parameter, 0, _area, values);

        Assert.All(values, value => Assert.InRange(value, -2 * Constants.ClimateUnit, 2 * Constants.ClimateUnit));
        Assert.True(values.Distinct().Count() > Side);
    }

    [Theory]
    [InlineData(ClimateParameter.Temperature)]
    [InlineData(ClimateParameter.Continentalness)]
    [InlineData(ClimateParameter.Depth)]
    public void GenerateClimate_AllOctavesAtACoarseScale_MatchesTheFullNoiseWithinRounding(ClimateParameter parameter)
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var full = new short[_coarseArea.SampleCount];
        var limited = new short[_coarseArea.SampleCount];

        generator.GenerateClimate(parameter, 0, _coarseArea, full);
        generator.GenerateClimate(parameter, AllOctaves, _coarseArea, limited);

        Assert.All(full.Zip(limited), pair => Assert.InRange(pair.First - pair.Second, -1, 1));
    }

    [Fact]
    public void GenerateClimate_FewerOctaves_GiveASmootherField()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var full = new short[_area.SampleCount];
        var widest = new short[_area.SampleCount];

        generator.GenerateClimate(ClimateParameter.Continentalness, AllOctaves, _area, full);
        generator.GenerateClimate(ClimateParameter.Continentalness, 1, _area, widest);

        Assert.True(GetRoughness(widest) < GetRoughness(full) / 2, $"Roughness {GetRoughness(widest)} against {GetRoughness(full)}.");
    }

    [Fact]
    public void GenerateClimate_Beta_GivesTemperatureAndHumidityBetweenZeroAndOne()
    {
        using var generator = CreateGenerator(McVersion.Beta1_7, Dimension.Overworld);
        var temperature = new short[_area.SampleCount];
        var humidity = new short[_area.SampleCount];

        generator.GenerateClimate(ClimateParameter.Temperature, 0, _area, temperature);
        generator.GenerateClimate(ClimateParameter.Humidity, 0, _area, humidity);

        Assert.All(temperature, value => Assert.InRange(value, 0, Constants.ClimateUnit));
        Assert.All(humidity, value => Assert.InRange(value, 0, Constants.ClimateUnit));
        Assert.True(temperature.Distinct().Count() > Side);
        Assert.NotEqual(temperature, humidity);
    }

    [Fact]
    public void GenerateBiomes_BetaWithoutOceans_ShowsTheClimateBiomesWhereTheOceansAre()
    {
        byte[] oceans = [0, 10];
        var area = new BiomeArea(CellScale, -Side, -Side, 2 * Side, 2 * Side, SurfaceY);
        var biomes = new byte[area.SampleCount];
        var landBiomes = new byte[area.SampleCount];
        using var generator = CreateGenerator(McVersion.Beta1_7, Dimension.Overworld);
        using var landGenerator = new CubiomesWorldGenerator(McVersion.Beta1_7, GeneratorFlags.NoBetaOcean);
        landGenerator.Apply(Dimension.Overworld, Seed);

        generator.GenerateBiomes(area, biomes);
        landGenerator.GenerateBiomes(area, landBiomes);

        Assert.Contains(biomes, biome => oceans.Contains(biome));
        Assert.DoesNotContain(landBiomes, biome => oceans.Contains(biome));
        Assert.All(biomes.Zip(landBiomes), pair => Assert.True(oceans.Contains(pair.First) || pair.First == pair.Second));
    }

    [Theory]
    [InlineData(McVersion.V1_16_5, Dimension.Overworld, ClimateParameter.Temperature)]
    [InlineData(McVersion.Beta1_7, Dimension.Overworld, ClimateParameter.Erosion)]
    [InlineData(McVersion.V1_21_3, Dimension.Nether, ClimateParameter.Temperature)]
    public void GenerateClimate_GeneratorWithoutTheParameter_IsRejected(McVersion version, Dimension dimension, ClimateParameter parameter)
    {
        using var generator = CreateGenerator(version, dimension);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.GenerateClimate(parameter, 0, _area, new short[_area.SampleCount]));

        Assert.Equal(NativeStatus.Unsupported, exception.Status);
    }

    [Theory]
    [InlineData(McVersion.V1_16_5)]
    [InlineData(McVersion.V1_12_2)]
    [InlineData(McVersion.V1_7_10)]
    public void GenerateLayer_River_CoversEveryRiverOfTheBiomeMap(McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld);
        var biomes = new byte[_area.SampleCount];
        var rivers = new byte[_area.SampleCount];

        generator.GenerateBiomes(_area, biomes);
        generator.GenerateLayer(GeneratorLayer.River, _area, rivers);

        Assert.Contains(River, biomes);
        Assert.All(rivers, value => Assert.True(value is River or NoBiome));
        Assert.All(biomes.Zip(rivers), pair => Assert.True(pair.First is not (River or FrozenRiver) || pair.Second == River));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    public void GenerateLayer_RiverAtACoarseScale_ShowsARiverWhereAnyCoveredCellHasOne(int scale)
    {
        const int coarseSide = 16;
        var cells = scale / CellScale;
        using var generator = CreateGenerator(McVersion.V1_16_5, Dimension.Overworld);
        var coarse = new BiomeArea(scale, -coarseSide / 2, -coarseSide / 2, coarseSide, coarseSide, SurfaceY);
        var fine = new BiomeArea(CellScale, coarse.X * cells, coarse.Z * cells, coarseSide * cells, coarseSide * cells, SurfaceY);
        var merged = new byte[coarse.SampleCount];
        var rivers = new byte[fine.SampleCount];

        generator.GenerateLayer(GeneratorLayer.River, coarse, merged);
        generator.GenerateLayer(GeneratorLayer.River, fine, rivers);

        Assert.Contains(River, merged);
        Assert.Contains(NoBiome, merged);
        for (var index = 0; index < merged.Length; index++)
        {
            var (left, top) = (index % coarseSide * cells, index / coarseSide * cells);
            var hasRiver = Enumerable.Range(0, cells * cells).Any(cell => rivers[(top + cell / cells) * fine.SizeX + left + cell % cells] == River);
            Assert.Equal(hasRiver ? River : NoBiome, merged[index]);
        }
    }

    [Fact]
    public void GenerateLayer_OceanTemperature_IsWarmWhereTheBiomeMapShowsAWarmOcean()
    {
        using var generator = CreateGenerator(McVersion.V1_16_5, Dimension.Overworld, SeedWithWarmOceans);
        var area = new BiomeArea(CellScale, -Side, -Side, 2 * Side, 2 * Side, SurfaceY);
        var biomes = new byte[area.SampleCount];
        var temperatures = new byte[area.SampleCount];

        generator.GenerateBiomes(area, biomes);
        generator.GenerateLayer(GeneratorLayer.OceanTemperature, area, temperatures);

        Assert.Contains(WarmOcean, biomes);
        Assert.All(biomes.Zip(temperatures), pair => Assert.True(pair.First != WarmOcean || pair.Second == WarmOcean));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void GenerateLayer_OceanTemperature_KnowsOnlyTheFiveOceanTemperaturesAtEveryScale(int scale)
    {
        byte[] oceans = [0, 10, WarmOcean, 45, 46];
        using var generator = CreateGenerator(McVersion.V1_16_5, Dimension.Overworld);
        var area = new BiomeArea(scale, -32, -32, 64, 64, SurfaceY);
        var temperatures = new byte[area.SampleCount];

        generator.GenerateLayer(GeneratorLayer.OceanTemperature, area, temperatures);

        Assert.All(temperatures, value => Assert.Contains(value, oceans));
        Assert.True(temperatures.Distinct().Count() > 1);
    }

    [Theory]
    [InlineData(McVersion.V1_12_2, GeneratorLayer.OceanTemperature, 4)]
    [InlineData(McVersion.V1_21_3, GeneratorLayer.River, 4)]
    [InlineData(McVersion.V1_16_5, GeneratorLayer.River, 256)]
    public void GenerateLayer_LayerTheGeneratorDoesNotHave_IsRejected(McVersion version, GeneratorLayer layer, int scale)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld);
        var area = new BiomeArea(scale, 0, 0, 16, 16, SurfaceY);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.GenerateLayer(layer, area, new byte[area.SampleCount]));

        Assert.Equal(NativeStatus.Unsupported, exception.Status);
    }

    [Theory]
    [InlineData(McVersion.V1_21_3)]
    [InlineData(McVersion.V1_16_5)]
    [InlineData(McVersion.V1_12_2)]
    public void GenerateHeights_Overworld_PutsDeepOceansBelowAndPlainsAboveTheSeaLevel(McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld, 0);
        var biomes = new byte[_area.SampleCount];
        var heights = new short[_area.SampleCount];

        generator.GenerateBiomes(_area, biomes);
        generator.GenerateHeights(_area, heights);

        Assert.True(GetMeanHeight(biomes, heights, DeepOcean) < SeaLevel - 10, $"Deep oceans lie at {GetMeanHeight(biomes, heights, DeepOcean)}.");
        Assert.True(GetMeanHeight(biomes, heights, Plains) > SeaLevel, $"Plains lie at {GetMeanHeight(biomes, heights, Plains)}.");
    }

    [Fact]
    public void GenerateHeights_CoarseScale_SamplesTheSameSurfaceAsTheFineScale()
    {
        using var generator = CreateGenerator(McVersion.V1_16_5, Dimension.Overworld);
        var coarse = new BiomeArea(16, -8, -8, 16, 16, SurfaceY);
        var fine = new BiomeArea(CellScale, -32, -32, 64, 64, SurfaceY);
        var coarseHeights = new short[coarse.SampleCount];
        var fineHeights = new short[fine.SampleCount];

        generator.GenerateHeights(coarse, coarseHeights);
        generator.GenerateHeights(fine, fineHeights);

        for (var z = 0; z < coarse.SizeZ; z++)
        {
            for (var x = 0; x < coarse.SizeX; x++)
                Assert.Equal(fineHeights[(z * 4 + 2) * fine.SizeX + x * 4 + 2], coarseHeights[z * coarse.SizeX + x]);
        }
    }

    [Fact]
    public void GenerateHeights_End_RaisesTheMainIslandOutOfTheVoid()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.End);
        var row = new BiomeArea(CellScale, 0, 0, 128, 1, 0);
        var heights = new short[row.SampleCount];

        generator.GenerateHeights(row, heights);

        Assert.True(heights[0] > 40 * Constants.HeightUnit, $"The main island lies at {heights[0] / (double)Constants.HeightUnit}.");
        Assert.Contains((short)0, heights);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    public void GenerateHeights_EndAtACoarseScale_RaisesTheMainIslandOutOfTheVoid(int scale)
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.End);
        var row = new BiomeArea(scale, 0, 0, 64, 1, 0);
        var heights = new short[row.SampleCount];

        generator.GenerateHeights(row, heights);

        Assert.True(heights[0] > 40 * Constants.HeightUnit, $"The main island lies at {heights[0] / (double)Constants.HeightUnit}.");
        Assert.Contains((short)0, heights);
    }

    [Theory]
    [InlineData(McVersion.V1_21_3, Dimension.Nether)]
    [InlineData(McVersion.Beta1_7, Dimension.Overworld)]
    [InlineData(McVersion.V1_8_9, Dimension.End)]
    public void GenerateHeights_GeneratorWithoutAHeightEstimate_IsRejected(McVersion version, Dimension dimension)
    {
        using var generator = CreateGenerator(version, dimension);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.GenerateHeights(_area, new short[_area.SampleCount]));

        Assert.Equal(NativeStatus.Unsupported, exception.Status);
    }

    private static CubiomesWorldGenerator CreateGenerator(McVersion version, Dimension dimension, long seed = Seed)
    {
        var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, seed);
        return generator;
    }

    private static double GetMeanHeight(byte[] biomes, short[] heights, byte biome)
    {
        var selected = heights.Where((_, index) => biomes[index] == biome).ToArray();
        Assert.NotEmpty(selected);
        return selected.Average(height => height / (double)Constants.HeightUnit);
    }

    /// <summary>The mean difference between horizontally neighbouring samples.</summary>
    private static double GetRoughness(short[] values)
        => Enumerable.Range(0, values.Length - 1).Where(index => (index + 1) % Side != 0).Average(index => Math.Abs(values[index + 1] - values[index]));
}
