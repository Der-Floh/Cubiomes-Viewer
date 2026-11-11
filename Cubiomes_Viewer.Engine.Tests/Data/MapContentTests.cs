using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Tests.Data;

public sealed class MapContentTests
{
    private const int SampleHeight = 70;
    private const int Octaves = 3;
    private static readonly WorldKey _world = new(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);

    [Theory]
    [InlineData(LayerMode.Biomes, SampleHeight, 0)]
    [InlineData(LayerMode.StructurePotential, SampleHeight, 0)]
    [InlineData(LayerMode.Temperature, 0, Octaves)]
    [InlineData(LayerMode.Depth, SampleHeight, Octaves)]
    [InlineData(LayerMode.Rivers, 0, 0)]
    [InlineData(LayerMode.Height, 0, 0)]
    public void For_Layer_KeepsOnlyTheHeightAndOctavesItReactsTo(LayerMode layer, int expectedY, int expectedOctaves)
    {
        var content = MapContent.For(_world, layer, SampleHeight, Octaves);

        var contentLayer = layer == LayerMode.StructurePotential ? LayerMode.Biomes : layer;
        Assert.Equal(new MapContent(_world, contentLayer, expectedY, expectedOctaves), content);
    }

    [Fact]
    public void For_LandBiomes_AsksForTheBiomesOfTheWorldWithoutItsOceans()
    {
        var beta = new WorldKey(262, McVersion.Beta1_7, Dimension.Overworld, GeneratorFlags.ForceOceanVariants);

        var content = MapContent.For(beta, LayerMode.LandBiomes, SampleHeight, Octaves);

        Assert.Equal(beta with { Flags = GeneratorFlags.ForceOceanVariants | GeneratorFlags.NoBetaOcean }, content.World);
        Assert.Equal(LayerMode.Biomes, content.Layer);
        Assert.Equal(0, content.Octaves);
    }

    [Fact]
    public void GetRequest_Tile_AsksForThatTileOfTheContent()
    {
        var content = new MapContent(_world, LayerMode.Depth, SampleHeight, Octaves);
        var tile = new TileKey(16, -2, 5);

        Assert.Equal(new TileRequest(_world, LayerMode.Depth, SampleHeight, tile, Octaves), content.GetRequest(tile));
    }
}
