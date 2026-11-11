using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Everything a map layer needs to draw one frame.</summary>
public readonly record struct MapScene(MapContent Content, MapViewport Viewport, double RenderScaling)
{
    public WorldKey World => Content.World;

    /// <summary>Rounds a horizontal or vertical position to the nearest device pixel boundary.</summary>
    public double Snap(double value) => Math.Round(value * RenderScaling) / RenderScaling;
}
