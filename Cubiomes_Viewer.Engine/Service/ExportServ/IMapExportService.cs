using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>Exports a layer of worlds as images: one PNG, or a ZIP archive of PNG images for several seeds or tiles.</summary>
public interface IMapExportService
{
    /// <summary>The blocks per pixel a layer of a world can be exported at, finest first.</summary>
    IReadOnlyList<int> GetScales(WorldKey world, LayerMode layer);

    /// <summary>Lists what keeps an export from being made; nothing when it can be.</summary>
    IReadOnlyList<string> FindProblems(MapExportRequest request);

    /// <summary>
    /// Writes the export into <paramref name="destination"/>, which receives only asynchronous writes. Rows of
    /// samples are generated on the compute workers and written as they come, so memory stays small for any size.
    /// </summary>
    /// <param name="request">An export without problems.</param>
    /// <param name="destination">The file to write.</param>
    /// <param name="progress">Receives the share written so far, from 0 to 1.</param>
    /// <param name="cancellationToken">Stops the export; what was written stays incomplete.</param>
    Task ExportAsync(MapExportRequest request, Stream destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
