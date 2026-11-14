using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Data;

/// <summary>
/// A thing on the map that can be picked: a marker, a pin the user put down, or a spot that only a ring marks. <see cref="Structure"/> is set for structures,
/// whose details the generator can describe, <see cref="Pin"/> for pins, which have no marker, and <see cref="Note"/> where the callout says more.
/// </summary>
public sealed record MapSelection(string Name, MapMarker? Marker, BlockPos Position, StructureType? Structure = null, MapPin? Pin = null, string? Note = null)
{
    public const string PinName = "Pin";
    public const string StrongholdName = "Stronghold";

    /// <summary>Whether this is a spot: a block with neither marker nor pin, which the map rings.</summary>
    public bool IsSpot => Marker is null && Pin is null;

    /// <summary>A structure, whose details the callout reads from the generator.</summary>
    public static MapSelection ForStructure(string name, MapMarker marker, BlockPos position, StructureType type, string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(marker);

        return new MapSelection(name, marker, position, type, Note: note);
    }

    /// <summary>A stronghold, named by its place in generation order as the map numbers them.</summary>
    public static MapSelection ForStronghold(int index, MapMarker marker, BlockPos position, string? note = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(marker);

        return new MapSelection($"{StrongholdName} {index + 1}", marker, position, Note: note);
    }

    public static MapSelection ForPin(MapPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return new MapSelection(pin.Name ?? PinName, null, new BlockPos(pin.X, pin.Z), Pin: pin);
    }

    /// <summary>A block that only a ring marks, with a note for its callout.</summary>
    public static MapSelection ForSpot(string name, BlockPos position, string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new MapSelection(name, null, position, Note: note);
    }
}
