using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.MeasurementServ;
using Cubiomes_Viewer.Engine.Tests.Service.SettingsServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.MeasurementServ;

public sealed class MeasurementServiceTests
{
    private readonly FakeSettingsService _settings = new();
    private readonly MeasurementService _measurements;
    private int _changes;

    public MeasurementServiceTests()
    {
        _measurements = new MeasurementService(_settings);
        _measurements.Changed += () => _changes++;
    }

    [Fact]
    public void Add_AMeasurement_StoresItForItsSeedAndDimensionOnly()
    {
        var measurement = _measurements.Add(262, Dimension.Overworld, new BlockPos(10, -20), new BlockPos(40, 20));
        _measurements.Add(262, Dimension.Nether, new BlockPos(0, 0), new BlockPos(5, 5));
        _measurements.Add(263, Dimension.Overworld, new BlockPos(0, 0), new BlockPos(5, 5));

        Assert.Equal([measurement], _measurements.GetMeasurements(262, Dimension.Overworld));
        Assert.Equal((new BlockPos(10, -20), new BlockPos(40, 20), 50.0, (string?)null), (measurement.Start, measurement.End, measurement.Length, measurement.Name));
        Assert.Equal(3, _settings.Current.Map.Measurements.Count);
        Assert.Equal((3, 3), (_changes, _settings.SaveRequests));
    }

    [Fact]
    public void Rename_WithSpaces_KeepsTheTrimmedNameAndBlankTakesItAway()
    {
        var measurement = _measurements.Add(262, Dimension.Overworld, new BlockPos(0, 0), new BlockPos(3, 4));

        _measurements.Rename(measurement, "  Rail line ");
        _measurements.Rename(measurement, "Rail line");
        Assert.Equal("Rail line", measurement.Name);

        _measurements.Rename(measurement, "   ");
        Assert.Null(measurement.Name);
        Assert.Equal((3, 3), (_changes, _settings.SaveRequests));
    }

    [Fact]
    public void Remove_AMeasurement_TakesItAwayAndIgnoresOneThatIsGone()
    {
        var measurement = _measurements.Add(262, Dimension.Overworld, new BlockPos(0, 0), new BlockPos(3, 4));

        _measurements.Remove(measurement);
        _measurements.Remove(measurement);

        Assert.Empty(_measurements.GetMeasurements(262, Dimension.Overworld));
        Assert.Equal((2, 2), (_changes, _settings.SaveRequests));
    }
}
