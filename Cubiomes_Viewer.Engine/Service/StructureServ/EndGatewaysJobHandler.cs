using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class EndGatewaysJobHandler : IJobHandler<EndGatewaysJob, EndGatewayData>
{
    public EndGatewayData Handle(EndGatewaysJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var generator = context.GetGenerator(job.World with { Dimension = Dimension.End });
        return new EndGatewayData { World = job.World, Gateways = generator.FindEndGateways() };
    }
}
