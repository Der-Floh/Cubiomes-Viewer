using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class QuadHutBaseScanJobHandler : IJobHandler<QuadHutBaseScanJob, QuadHutBaseScan>
{
    private const int MaxBasesPerScan = 1 << 14;

    private readonly IQuadFinder _finder;

    public QuadHutBaseScanJobHandler(IQuadFinder finder)
    {
        _finder = finder;
    }

    public QuadHutBaseScan Handle(QuadHutBaseScanJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var bases = new ulong[MaxBasesPerScan];
        var count = _finder.ScanQuadHutBases(job.LowBits, job.FirstHigh, job.Count, bases);
        return new QuadHutBaseScan { Bases = bases[..count] };
    }
}
