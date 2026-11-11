using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>Provides the icons and colours of the map overlays from the application resources and draws markers with them.</summary>
public interface IMapStyleService
{
    /// <summary>The side length of a drawn marker in device-independent pixels.</summary>
    double MarkerSize { get; }

    MapMarker Spawn { get; }
    MapMarker Stronghold { get; }
    Color SlimeChunkColor { get; }
    Color MeasureColor { get; }
    Color OutlineColor { get; }
    Color SelectionColor { get; }

    MapMarker GetStructureMarker(StructureType type);

    /// <summary>Draws a marker centred on a point.</summary>
    void DrawMarker(DrawingContext context, MapMarker marker, Point center);
}
