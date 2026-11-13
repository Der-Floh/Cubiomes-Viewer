using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Data;

/// <summary>A dimension offered in a picker, shown with its badge.</summary>
public sealed class DimensionChoice
{
    public required string Display { get; init; }
    public required Dimension Value { get; init; }
    public required MapMarker Marker { get; init; }

    public override string ToString() => Display;
}
