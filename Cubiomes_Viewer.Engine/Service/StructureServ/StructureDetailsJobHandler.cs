using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureDetailsJobHandler : IJobHandler<StructureDetailsJob, StructureDetails>
{
    public StructureDetails Handle(StructureDetailsJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        return context.GetGenerator(job.World).DescribeStructure(job.Type, job.Position);
    }
}
