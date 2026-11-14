using Avalonia.Media;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Draws the line of a distance measurement in progress, from its start to the block under the pointer, together with its length.</summary>
public sealed class MeasureLayer : IMapLayer
{
    private static readonly RenderOptions _renderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>The block the distance is measured from, or <see langword="null"/> when nothing is measured.</summary>
    public BlockPos? Start
    {
        get;
        set
        {
            field = value;
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The block the distance is measured to, usually the one under the pointer.</summary>
    public BlockPos? End
    {
        get;
        set
        {
            field = value;
            if (Start is not null)
                Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private readonly MeasurePainter _painter;

    public MeasureLayer(MeasurePainter painter)
    {
        _painter = painter;
    }

    public void Attach() { }
    public void Detach() { }
    public void UpdateScene(MapScene scene) { }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || Start is not { } start)
            return;

        using (context.PushRenderOptions(_renderOptions))
            _painter.DrawOpen(context, scene.Viewport, start, End);
    }
}
