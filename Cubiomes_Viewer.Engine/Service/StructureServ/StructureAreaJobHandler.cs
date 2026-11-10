using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureAreaJobHandler : IJobHandler<StructureAreaJob, StructureAreaData>
{
    public StructureAreaData Handle(StructureAreaJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var request = job.Request;
        var positions = context.GetGenerator(request.World).FindStructures(request.Type, request.Area);
        return new StructureAreaData { Request = request, Positions = positions };
    }
}
