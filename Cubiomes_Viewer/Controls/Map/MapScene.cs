using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Everything a map layer needs to draw one frame: the open world, the tiles its shown layer is painted on, the view,
/// and the device pixels the map lands on.
/// </summary>
public readonly record struct MapScene(WorldKey World, MapContent Content, MapViewport Viewport, PixelGrid Pixels);
