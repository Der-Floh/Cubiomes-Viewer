using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Data;

/// <summary>
/// A marker on the map that can be picked. <see cref="Structure"/> is set for structures, whose details the generator can describe.
/// </summary>
public sealed record MapSelection(string Name, MapMarker Marker, BlockPos Position, StructureType? Structure = null);
