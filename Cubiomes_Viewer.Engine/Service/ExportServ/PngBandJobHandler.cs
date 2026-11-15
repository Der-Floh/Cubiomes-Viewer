using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>Filters and deflates one band of a PNG image on a worker.</summary>
public sealed class PngBandJobHandler : IJobHandler<PngBandJob, PngBand>
{
    public PngBand Handle(PngBandJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        return PngBandCompressor.Compress(job.Rows, job.RowBytes, job.BytesPerPixel, job.PreviousRow, job.ColorType, job.IsLast, cancellationToken);
    }
}
