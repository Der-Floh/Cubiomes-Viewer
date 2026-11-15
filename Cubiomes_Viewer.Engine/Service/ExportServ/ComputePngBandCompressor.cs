using System.Buffers;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// Compresses bands as jobs on the workers of the compute service, for runtimes such as the browser's, whose exporting
/// thread has no other threads to share the work with.
/// </summary>
internal sealed class ComputePngBandCompressor(IComputeService compute, JobPriority priority) : IPngBandCompressor
{
    public int Concurrency => Math.Max(1, compute.WorkerCount);

    public Task<PngBand> CompressAsync(PngBandRows band, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(band);

        var rows = band.Span.ToArray();
        ArrayPool<byte>.Shared.Return(band.Rows);
        return compute.RunAsync(new PngBandJob(rows, band.RowBytes, band.BytesPerPixel, band.PreviousRow, band.ColorType, band.IsLast), priority, cancellationToken);
    }
}
