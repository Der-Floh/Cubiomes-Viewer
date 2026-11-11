using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Everything a map layer needs to draw one frame: the open world, the tiles its shown layer is painted on, and the view.
/// </summary>
public readonly record struct MapScene(WorldKey World, MapContent Content, MapViewport Viewport, double RenderScaling)
{
    /// <summary>Rounds a horizontal or vertical position to the nearest device pixel boundary.</summary>
    public double Snap(double value) => Math.Round(value * RenderScaling) / RenderScaling;
}
