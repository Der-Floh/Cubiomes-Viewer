using Cubiomes_Viewer.Engine.Data.Export;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>Filters and deflates the bands of a PNG image away from the thread that writes it.</summary>
internal interface IPngBandCompressor
{
    /// <summary>How many bands are worth compressing at the same time.</summary>
    int Concurrency { get; }

    /// <summary>Compresses a band, and gives its rented rows back to the shared pool once it no longer needs them.</summary>
    Task<PngBand> CompressAsync(PngBandRows band, CancellationToken cancellationToken);
}
