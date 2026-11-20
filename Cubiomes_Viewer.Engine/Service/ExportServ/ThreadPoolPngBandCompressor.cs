using System.Buffers;

using Cubiomes_Viewer.Engine.Data.Export;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>Compresses bands on the thread pool, as many at a time as there are processors.</summary>
internal sealed class ThreadPoolPngBandCompressor : IPngBandCompressor
{
    public static ThreadPoolPngBandCompressor Instance { get; } = new();

    public int Concurrency => Environment.ProcessorCount;

    public Task<PngBand> CompressAsync(PngBandRows band, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(band);

        return Task.Run(() =>
        {
            try
            {
                return PngBandCompressor.Compress(band.Span, band.RowBytes, band.BytesPerPixel, band.PreviousRow, band.TriesFilters, band.IsLast, cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(band.Rows);
            }
        }, CancellationToken.None);
    }
}
