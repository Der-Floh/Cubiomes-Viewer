using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Data;

/// <summary>
/// A thing on the map that can be picked: a marker, a pin or a kept measurement of the user's, or a spot that a pin marks while it
/// is picked. <see cref="Structure"/> is set for structures, whose details the generator can describe, <see cref="Pin"/> for pins,
/// <see cref="Measurement"/> for measurements, which have no marker, <see cref="SpotLook"/> for spots, and <see cref="Note"/> where
/// the callout says more.
/// </summary>
public sealed record MapSelection(string Name, MapMarker? Marker, BlockPos Position, StructureType? Structure = null, MapPin? Pin = null, string? Note = null,
    MapMeasurement? Measurement = null, PinLook? SpotLook = null)
{
    public const string PinName = "Pin";
    public const string SpawnName = "World spawn";
    public const string StrongholdName = "Stronghold";
    public const string MeasurementName = "Measurement";

    /// <summary>Whether this is a spot: a block nothing on the map marks, so a pin of <see cref="SpotLook"/> does while it is picked.</summary>
    public bool IsSpot => SpotLook is not null;

    /// <summary>A kept measurement, placed at its start.</summary>
    public static MapSelection ForMeasurement(MapMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        return new MapSelection(measurement.Name ?? MeasurementName, null, measurement.Start, Measurement: measurement);
    }

    /// <summary>A structure, whose details the callout reads from the generator.</summary>
    public static MapSelection ForStructure(string name, MapMarker marker, BlockPos position, StructureType type, string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(marker);

        return new MapSelection(name, marker, position, type, Note: note);
    }

    /// <summary>A stronghold, named by its place in generation order as the map numbers them, whose rooms the callout reads like a structure's.</summary>
    public static MapSelection ForStronghold(int index, MapMarker marker, BlockPos position, string? note = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(marker);

        return new MapSelection($"{StrongholdName} {index + 1}", marker, position, StructureType.Stronghold, Note: note);
    }

    public static MapSelection ForSpawn(MapMarker marker, BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(marker);
        return new MapSelection(SpawnName, marker, position);
    }

    public static MapSelection ForPin(MapPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return new MapSelection(pin.Name ?? PinName, null, new BlockPos(pin.X, pin.Z), Pin: pin);
    }

    /// <summary>A block nothing on the map marks: a pin of <paramref name="look"/> shows it, and its callout the badge and a note.</summary>
    public static MapSelection ForSpot(string name, BlockPos position, string note, MapMarker marker, PinLook look)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(marker);
        ArgumentNullException.ThrowIfNull(look);

        return new MapSelection(name, marker, position, Note: note, SpotLook: look);
    }
}
