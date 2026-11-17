using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureDetailsJobHandler : IJobHandler<StructureDetailsJob, StructureDetails>
{
    private readonly IStructureHeightService _heights;

    public StructureDetailsJobHandler(IStructureHeightService heights)
    {
        _heights = heights;
    }

    public StructureDetails Handle(StructureDetailsJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var generator = context.GetGenerator(job.World);
        var details = generator.DescribeStructure(job.Type, job.Position);
        return details.WithHeight(_heights.Find(generator, job.World.Dimension, details));
    }
}
