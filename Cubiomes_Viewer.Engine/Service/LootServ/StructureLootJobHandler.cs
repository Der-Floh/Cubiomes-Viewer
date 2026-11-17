using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.LootServ;

public sealed class StructureLootJobHandler : IJobHandler<StructureLootJob, StructureLoot>
{
    public StructureLoot Handle(StructureLootJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        return context.GetGenerator(job.World).FindLoot(job.Type, job.Position) ?? StructureLoot.Unknown;
    }
}
