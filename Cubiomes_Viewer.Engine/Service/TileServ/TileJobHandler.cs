using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>Generates the samples of one tile in the layer its request names.</summary>
public sealed class TileJobHandler : IJobHandler<TileJob, TileData>
{
    public TileData Handle(TileJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var request = job.Request;
        var generator = context.GetGenerator(request.World);
        var area = TilePyramid.GetArea(request.Tile, job.Samples, request.Y);

        return request.Layer.HasBiomeSamples
            ? new TileData { Request = request, Samples = job.Samples, Biomes = GenerateBiomes(generator, request.Layer, area) }
            : new TileData { Request = request, Samples = job.Samples, Values = GenerateValues(generator, request, area) };
    }

    private static byte[] GenerateBiomes(IWorldGenerator generator, LayerMode layer, BiomeArea area)
    {
        var biomes = new byte[area.SampleCount];
        if (layer.ToGeneratorLayer() is { } generatorLayer)
            generator.GenerateLayer(generatorLayer, area, biomes);
        else
            generator.GenerateBiomes(area, biomes);
        return biomes;
    }

    private static short[] GenerateValues(IWorldGenerator generator, TileRequest request, BiomeArea area)
    {
        var values = new short[area.SampleCount];
        if (request.Layer.ToClimateParameter() is { } parameter)
            generator.GenerateClimate(parameter, request.Octaves, area, values);
        else if (request.Layer == LayerMode.Height)
            generator.GenerateHeights(area, values);
        else
            throw new NotSupportedException($"The layer '{request.Layer}' has no samples to generate.");
        return values;
    }
}
