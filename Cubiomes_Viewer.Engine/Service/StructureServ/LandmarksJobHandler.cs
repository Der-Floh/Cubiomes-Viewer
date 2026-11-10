using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class LandmarksJobHandler : IJobHandler<LandmarksJob, WorldLandmarks>
{
    public WorldLandmarks Handle(LandmarksJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var generator = context.GetGenerator(job.World with { Dimension = Dimension.Overworld });
        return new WorldLandmarks
        {
            World = job.World,
            Spawn = generator.FindSpawn(),
            Strongholds = generator.FindStrongholds(),
        };
    }
}
