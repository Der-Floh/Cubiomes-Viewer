using Avalonia.Media;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>One drawable layer of the map (tiles, grid, overlays). Layers are drawn in the order they were added.</summary>
public interface IMapLayer
{
    /// <summary>Raised on the UI thread when the layer has new content to draw.</summary>
    event EventHandler? Invalidated;

    bool IsVisible { get; set; }

    void Attach();
    void Detach();

    /// <summary>Called when the world or viewport changed, before the next render.</summary>
    void UpdateScene(MapScene scene);

    void Render(DrawingContext context, MapScene scene);
}
