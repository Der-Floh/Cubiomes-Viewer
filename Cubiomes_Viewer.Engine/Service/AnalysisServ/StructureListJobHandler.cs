using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Service.AnalysisServ;

/// <summary>Lists the structures of one type that generate in an area, in the dimension the type generates in.</summary>
public sealed class StructureListJobHandler(IStructureFinder structureFinder) : IJobHandler<StructureListJob, StructureListing>
{
    public StructureListing Handle(StructureListJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var request = job.Request;
        if (!structureFinder.TryGetPlacement(request.Type, request.World.Version, out var placement))
            return new StructureListing { Type = request.Type, Entries = [] };

        var generator = context.GetGenerator(request.World with { Dimension = placement.Dimension });
        var positions = generator.FindStructures(request.Type, request.Area.ToBlocks(request.Reference))
            .Where(position => request.Area.Contains(request.Reference, position))
            .OrderBy(position => position.SquaredDistanceTo(request.Reference))
            .ThenBy(position => position.Z)
            .ThenBy(position => position.X);

        List<StructureEntry> entries = [];
        foreach (var position in positions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new StructureEntry(position, request.Describe ? generator.DescribeStructure(request.Type, position) : null));
        }

        return new StructureListing { Type = request.Type, Entries = [.. entries] };
    }
}
