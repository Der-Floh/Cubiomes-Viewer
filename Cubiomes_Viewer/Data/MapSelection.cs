using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Data;

/// <summary>
/// A thing on the map that can be picked: a marker, or a pin the user put down. <see cref="Structure"/> is set for structures,
/// whose details the generator can describe, and <see cref="Pin"/> for pins, which have no marker.
/// </summary>
public sealed record MapSelection(string Name, MapMarker? Marker, BlockPos Position, StructureType? Structure = null, MapPin? Pin = null)
{
    public const string PinName = "Pin";

    public static MapSelection ForPin(MapPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return new MapSelection(PinName, null, new BlockPos(pin.X, pin.Z), Pin: pin);
    }
}
