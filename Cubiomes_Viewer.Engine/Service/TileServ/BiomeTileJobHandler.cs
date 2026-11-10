using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public sealed class BiomeTileJobHandler : IJobHandler<BiomeTileJob, TileData>
{
    public TileData Handle(BiomeTileJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var request = job.Request;
        var generator = context.GetGenerator(request.World);
        var area = TilePyramid.GetArea(request.Tile, job.Samples, request.Y);
        var biomes = new byte[area.SampleCount];
        generator.GenerateBiomes(area, biomes);

        return new TileData { Request = request, Samples = job.Samples, Biomes = biomes };
    }
}
