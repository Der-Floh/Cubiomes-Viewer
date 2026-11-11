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

    private readonly LayerCatalog _catalog = new();
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

                Assert.Equal(request, tile.Request);
                Assert.Equal(layer.Mode.HasBiomeSamples ? Samples * Samples : 0, tile.Biomes.Length);
                Assert.Equal(layer.Mode.HasBiomeSamples ? 0 : Samples * Samples, tile.Values.Length);
                Assert.Equal(Samples * Samples * (layer.Mode.HasBiomeSamples ? sizeof(byte) : sizeof(short)), tile.ByteSize);
            }
        }
    }
}
