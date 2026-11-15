using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class TileJobHandlerTests
{
    private const int Samples = 3;
    private const int SampleHeight = 64;

    private readonly LayerCatalog _catalog = TestEngine.CreateLayerCatalog();
    private readonly TileJobHandler _handler = new();

    [Theory]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld)]
    [InlineData(McVersion.V1_21_3, Dimension.Nether)]
    [InlineData(McVersion.V1_21_3, Dimension.End)]
    [InlineData(McVersion.V1_16_5, Dimension.Overworld)]
    [InlineData(McVersion.V1_12_2, Dimension.Overworld)]
    [InlineData(McVersion.Beta1_8, Dimension.Overworld)]
    [InlineData(McVersion.Beta1_7, Dimension.Overworld)]
    public void Handle_EveryAvailableLayerAtEveryScaleOfItsPyramid_ProducesATile(McVersion version, Dimension dimension)
    {
        var world = new WorldKey(262, version, dimension, GeneratorFlags.None);
        using var context = new WorkerContext(new CubiomesWorldGeneratorFactory());

        foreach (var layer in _catalog.GetAvailable(version, dimension))
        {
            foreach (var scale in TileMetrics.For(world, layer.Mode).Scales)
            {
                var request = MapContent.For(world, layer.Mode, SampleHeight, octaves: 0).GetRequest(new TileKey(scale, -1, 1));

                var tile = _handler.Handle(new TileJob(request, Samples), context, CancellationToken.None);

                var border = layer.Mode == LayerMode.Height ? Constants.HeightTileBorder : 0;
                Assert.Equal(request, tile.Request);
                Assert.Equal(border, tile.Border);
                Assert.Equal(layer.Mode.HasBiomeSamples ? Samples * Samples : 0, tile.Biomes.Length);
                Assert.Equal(layer.Mode.HasBiomeSamples ? 0 : tile.Stride * tile.Stride, tile.Values.Length);
                Assert.Equal(layer.Mode.HasBiomeSamples ? Samples * Samples : tile.Values.Length * sizeof(short), tile.ByteSize);
            }
        }
    }

    [Theory]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld, 4)]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld, 64)]
    [InlineData(McVersion.V1_16_5, Dimension.Overworld, 4)]
    [InlineData(McVersion.V1_16_5, Dimension.Overworld, 16)]
    [InlineData(McVersion.V1_21_3, Dimension.End, 4)]
    [InlineData(McVersion.V1_21_3, Dimension.End, 16)]
    public void Handle_HeightTilesSideBySide_KeepTheSamplesOfTheOtherInTheirBorders(McVersion version, Dimension dimension, int scale)
    {
        const int Border = Constants.HeightTileBorder;
        var content = MapContent.For(new WorldKey(262, version, dimension, GeneratorFlags.None), LayerMode.Height, 0, octaves: 0);
        using var context = new WorkerContext(new CubiomesWorldGeneratorFactory());

        var west = _handler.Handle(new TileJob(content.GetRequest(new TileKey(scale, -1, 2)), Samples), context, CancellationToken.None);
        var east = _handler.Handle(new TileJob(content.GetRequest(new TileKey(scale, 0, 2)), Samples), context, CancellationToken.None);
        var south = _handler.Handle(new TileJob(content.GetRequest(new TileKey(scale, -1, 3)), Samples), context, CancellationToken.None);

        for (var along = -Border; along < Samples + Border; along++)
        {
            for (var into = -Border; into < Border; into++)
            {
                Assert.Equal(east.GetValue(into, along), west.GetValue(Samples + into, along));
                Assert.Equal(south.GetValue(along, into), west.GetValue(along, Samples + into));
            }
        }
    }
}
