using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.MeasurementServ;

/// <summary>Keeps the distances the user measured on the maps of seeds in the settings, so they are there again next time.</summary>
public interface IMeasurementService
{
    /// <summary>Raised after a measurement was kept, renamed or taken away.</summary>
    event Action? Changed;

    /// <summary>The measurements on the map of one dimension of a seed, oldest first.</summary>
    IReadOnlyList<MapMeasurement> GetMeasurements(long seed, Dimension dimension);

    MapMeasurement Add(long seed, Dimension dimension, BlockPos start, BlockPos end);

    /// <summary>Names a measurement; a name of nothing but white space takes the name away.</summary>
    void Rename(MapMeasurement measurement, string? name);

    void Remove(MapMeasurement measurement);
}
