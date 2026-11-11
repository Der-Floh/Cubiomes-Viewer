using Avalonia;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>A layer whose markers can be picked with the pointer.</summary>
public interface IMarkerLayer : IMapLayer
{
    /// <summary>Returns the marker drawn at a position of the map control, or <see langword="null"/> when there is none.</summary>
    MapSelection? HitTest(MapScene scene, Point position);
}
