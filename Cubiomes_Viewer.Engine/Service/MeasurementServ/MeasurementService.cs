using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SettingsServ;

namespace Cubiomes_Viewer.Engine.Service.MeasurementServ;

public sealed class MeasurementService : IMeasurementService
{
    public event Action? Changed;

    private readonly ISettingsService _settingsService;

    private List<MapMeasurement> Measurements => _settingsService.Current.Map.Measurements;

    public MeasurementService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<MapMeasurement> GetMeasurements(long seed, Dimension dimension)
        => [.. Measurements.Where(measurement => measurement.Seed == seed && measurement.Dimension == dimension)];

    public MapMeasurement Add(long seed, Dimension dimension, BlockPos start, BlockPos end)
    {
        var measurement = new MapMeasurement { Seed = seed, Dimension = dimension, StartX = start.X, StartZ = start.Z, EndX = end.X, EndZ = end.Z };
        Measurements.Add(measurement);
        StoreChange();
        return measurement;
    }

    public void Rename(MapMeasurement measurement, string? name)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        var trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (string.Equals(measurement.Name, trimmed, StringComparison.Ordinal))
            return;

        measurement.Name = trimmed;
        StoreChange();
    }

    public void Remove(MapMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        if (Measurements.Remove(measurement))
            StoreChange();
    }

    private void StoreChange()
    {
        _settingsService.RequestSave();
        Changed?.Invoke();
    }
}
