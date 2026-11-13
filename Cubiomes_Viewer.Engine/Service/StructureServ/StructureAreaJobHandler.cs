using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureAreaJobHandler : IJobHandler<StructureAreaJob, StructureAreaData>
{
    private readonly IStructureFeatureCatalog _features;

    public StructureAreaJobHandler(IStructureFeatureCatalog features)
    {
        _features = features;
    }

    public StructureAreaData Handle(StructureAreaJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var request = job.Request;
        var generator = context.GetGenerator(request.World);
        var positions = generator.FindStructures(request.Type, request.Area);
        return new StructureAreaData { Request = request, Positions = positions, Options = GetOptions(generator, request, positions) };
    }

    /// <summary>Describes each structure of a type with features, so that the map can hide structures by them without asking again.</summary>
    private ulong[] GetOptions(IWorldGenerator generator, StructureAreaRequest request, BlockPos[] positions)
    {
        var version = request.World.Version;
        if (_features.GetFeatures(request.Type, version).Count == 0)
            return [];

        return Array.ConvertAll(positions, position => _features.GetOptions(generator.DescribeStructure(request.Type, position), version));
    }
}
