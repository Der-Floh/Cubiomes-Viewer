using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Highlights the picked marker and outlines the boxes that belong to it, such as the pieces of a structure.</summary>
public sealed class SelectionLayer : IMapLayer
{
    private const double RingThickness = 3;
    private const double RingPadding = 5;
    private const double BoxThickness = 1.5;
    private const byte BoxFillOpacity = 48;
    private static readonly RenderOptions _renderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>The position of the picked marker, or <see langword="null"/> when nothing is picked.</summary>
    public BlockPos? Position
    {
        get;
        set
        {
            field = value;
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Boxes of blocks to outline around the picked marker.</summary>
    public IReadOnlyList<BlockBox> Boxes
    {
        get;
        set
        {
            field = value;
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    private readonly IMapStyleService _styleService;

    private ImmutableSolidColorBrush LineBrush => field ??= new ImmutableSolidColorBrush(_styleService.SelectionColor);
    private ImmutableSolidColorBrush FillBrush => field ??= new ImmutableSolidColorBrush(_styleService.SelectionColor, BoxFillOpacity / (double)byte.MaxValue);
    private IPen RingPen => field ??= new ImmutablePen(LineBrush, RingThickness);
    private IPen RingOutlinePen => field ??= new ImmutablePen(new ImmutableSolidColorBrush(_styleService.OutlineColor), RingThickness + 2);
    private IPen BoxPen => field ??= new ImmutablePen(LineBrush, BoxThickness);

    public SelectionLayer(IMapStyleService styleService)
    {
        _styleService = styleService;
    }

    public void Attach() { }
    public void Detach() { }
    public void UpdateScene(MapScene scene) { }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || Position is not { } position)
            return;

        using (context.PushRenderOptions(_renderOptions))
        {
            foreach (var box in Boxes)
                context.DrawRectangle(FillBrush, BoxPen, ToScreen(scene, box));

            var (x, y) = scene.Viewport.ToScreen(position.X, position.Z);
            var radius = _styleService.MarkerSize / 2 + RingPadding;
            context.DrawEllipse(null, RingOutlinePen, new Point(x, y), radius, radius);
            context.DrawEllipse(null, RingPen, new Point(x, y), radius, radius);
        }
    }

    private static Rect ToScreen(MapScene scene, BlockBox box)
    {
        var (left, top) = scene.Viewport.ToScreen(box.MinX, box.MinZ);
        var (right, bottom) = scene.Viewport.ToScreen(box.MaxX + 1.0, box.MaxZ + 1.0);
        return new Rect(new Point(left, top), new Point(right, bottom));
    }
}
