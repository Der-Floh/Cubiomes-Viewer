using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.LayerServ;

public sealed class LayerCatalogTests
{
    private const int MoreOctavesThanAnyNoiseHas = 64;
    private static readonly BiomeArea _area = new(4, -32, -32, 64, 64, 16);

    private readonly LayerCatalog _catalog = TestEngine.CreateLayerCatalog();

    public static TheoryData<McVersion, Dimension, LayerMode[]> AvailableLayers() => new()
    {
        {
            McVersion.V1_21_3, Dimension.Overworld,
            [LayerMode.Biomes, LayerMode.Temperature, LayerMode.Humidity, LayerMode.Continentalness, LayerMode.Erosion, LayerMode.Depth, LayerMode.Weirdness, LayerMode.Height]
        },
        { McVersion.V1_18_2, Dimension.Nether, [LayerMode.Biomes] },
        { McVersion.V1_21_3, Dimension.End, [LayerMode.Biomes, LayerMode.Height] },
        { McVersion.V1_17_1, Dimension.Overworld, [LayerMode.Biomes, LayerMode.Rivers, LayerMode.OceanTemperature, LayerMode.Height] },
        { McVersion.V1_13_2, Dimension.Overworld, [LayerMode.Biomes, LayerMode.Rivers, LayerMode.OceanTemperature, LayerMode.Height] },
        { McVersion.V1_12_2, Dimension.Overworld, [LayerMode.Biomes, LayerMode.Rivers, LayerMode.Height] },
        { McVersion.Beta1_8, Dimension.Overworld, [LayerMode.Biomes, LayerMode.Rivers, LayerMode.Height] },
        { McVersion.V1_8_9, Dimension.End, [LayerMode.Biomes] },
        { McVersion.V1_9_4, Dimension.End, [LayerMode.Biomes, LayerMode.Height] },
        { McVersion.Beta1_7, Dimension.Overworld, [LayerMode.Biomes, LayerMode.Temperature, LayerMode.Humidity] },
    };

    [Theory]
    [MemberData(nameof(AvailableLayers))]
    public void GetAvailable_VersionAndDimension_ListsTheLayersOfItsGenerator(McVersion version, Dimension dimension, LayerMode[] expected)
    {
        var layers = _catalog.GetAvailable(version, dimension);
        var ownLayers = layers.Select(layer => layer.Mode).Where(mode => mode != LayerMode.StructurePotential).ToArray();

        Assert.Equal(expected, ownLayers);
        Assert.All(expected, mode => Assert.True(_catalog.IsAvailable(mode, version, dimension)));
        Assert.All(Enum.GetValues<LayerMode>().Except(layers.Select(layer => layer.Mode)), mode => Assert.False(_catalog.IsAvailable(mode, version, dimension)));
        Assert.Same(layers, _catalog.GetAvailable(version, dimension));
    }

    [Theory]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld, true)]
    [InlineData(McVersion.V1_21_3, Dimension.Nether, true)]
    [InlineData(McVersion.V1_21_3, Dimension.End, true)]
    [InlineData(McVersion.V1_8_9, Dimension.End, false)]
    [InlineData(McVersion.Beta1_7, Dimension.Overworld, false)]
    public void IsAvailable_StructurePotential_NeedsAStructureWhoseBiomesAreKnown(McVersion version, Dimension dimension, bool expected)
    {
        Assert.Equal(expected, _catalog.IsAvailable(LayerMode.StructurePotential, version, dimension));
        Assert.Equal(expected, TestEngine.CreateStructureCatalog().GetWithBiomeRule(version, dimension).Count > 0);
        Assert.Equal(expected, _catalog.GetAvailable(version, dimension).Any(layer => layer.Mode == LayerMode.StructurePotential));
    }

    [Fact]
    public void GetInfo_EveryLayer_HasANameAndColoursExactlyWhenItHoldsNumbers()
    {
        foreach (var mode in Enum.GetValues<LayerMode>())
        {
            var info = _catalog.GetInfo(mode, McVersion.V1_21_3, Dimension.Overworld);

            Assert.Equal(mode, info.Mode);
            Assert.False(string.IsNullOrWhiteSpace(info.Name));
            Assert.Equal(mode.HasBiomeSamples, info.Values is null);
        }
    }

    [Fact]
    public void GetInfo_NamesAreUnique()
    {
        var names = Enum.GetValues<LayerMode>().Select(mode => _catalog.GetInfo(mode, McVersion.V1_21_3, Dimension.Overworld).Name).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData(LayerMode.Temperature)]
    [InlineData(LayerMode.Humidity)]
    public void GetInfo_BetaClimate_IsMeasuredFromZeroToOneAndHasNoOctavesToChoose(LayerMode mode)
    {
        var info = _catalog.GetInfo(mode, McVersion.Beta1_7, Dimension.Overworld);

        Assert.Equal(0, info.Values?.Ramp.Minimum);
        Assert.Equal(1, info.Values?.Ramp.Maximum);
        Assert.Equal(Constants.ClimateUnit, info.Values?.Unit);
        Assert.Equal(0, info.MaxOctaves);
    }

    [Fact]
    public void GetInfo_Height_UsesBlocksAndItsOwnColoursInTheEnd()
    {
        var overworld = _catalog.GetInfo(LayerMode.Height, McVersion.V1_21_3, Dimension.Overworld);
        var end = _catalog.GetInfo(LayerMode.Height, McVersion.V1_21_3, Dimension.End);

        Assert.Equal(Constants.HeightUnit, overworld.Values?.Unit);
        Assert.Equal(-64, overworld.Values?.Ramp.Minimum);
        Assert.Equal(0, end.Values?.Ramp.Minimum);
        Assert.NotSame(overworld.Values?.Ramp, end.Values?.Ramp);
        Assert.Equal(4.5, overworld.Values?.ToValue(72));
    }

    [Theory]
    [InlineData(LayerMode.Temperature)]
    [InlineData(LayerMode.Humidity)]
    [InlineData(LayerMode.Continentalness)]
    [InlineData(LayerMode.Erosion)]
    [InlineData(LayerMode.Depth)]
    [InlineData(LayerMode.Weirdness)]
    public void GetInfo_ClimateLayer_KnowsHowManyOctavesItsNoiseHas(LayerMode mode)
    {
        var parameter = Assert.IsType<ClimateParameter>(mode.ToClimateParameter());
        var octaves = _catalog.GetInfo(mode, McVersion.V1_21_3, Dimension.Overworld).MaxOctaves;
        using var generator = new CubiomesWorldGenerator(McVersion.V1_21_3, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, 262);

        var all = Generate(generator, parameter, MoreOctavesThanAnyNoiseHas);

        Assert.Equal(all, Generate(generator, parameter, octaves));
        Assert.NotEqual(all, Generate(generator, parameter, octaves - 1));
    }

    private static short[] Generate(CubiomesWorldGenerator generator, ClimateParameter parameter, int octaves)
    {
        var values = new short[_area.SampleCount];
        generator.GenerateClimate(parameter, octaves, _area, values);
        return values;
    }
}
