using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Draws the picked marker or pin larger than the others and above them, and outlines the boxes that belong to it, such as the pieces of a structure.</summary>
public sealed class SelectionLayer : IMapLayer
{
    /// <summary>How many times its normal size the picked marker or pin is drawn.</summary>
    public const double Magnification = 1.5;

    private const double BoxThickness = 1.5;
    private const byte BoxFillOpacity = 48;
    private static readonly RenderOptions _renderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>The picked marker or pin, or <see langword="null"/> when nothing is picked.</summary>
    public MapSelection? Picked
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
        if (!IsVisible || Picked is not { } picked)
            return;

        using (context.PushRenderOptions(_renderOptions))
        {
            foreach (var box in Boxes)
                context.DrawRectangle(FillBrush, BoxPen, ToScreen(scene, box));

            if (picked.Pin is { } pin)
                _styleService.DrawPin(context, _styleService.GetPinLook(pin.Icon), PinLayer.GetTip(scene.Viewport, pin), scene.Pixels, Magnification);
            else if (picked.Marker is { } marker)
                _styleService.DrawMarker(context, marker, ToScreen(scene.Viewport, picked), scene.Pixels, Magnification);
        }
    }

    /// <summary>Where a picked marker or pin is drawn on a view of the map.</summary>
    public Rect GetMarkerBounds(MapViewport viewport, MapSelection picked)
    {
        ArgumentNullException.ThrowIfNull(picked);

        if (picked.Pin is { } pin)
            return _styleService.GetPinBounds(PinLayer.GetTip(viewport, pin), Magnification);

        var center = ToScreen(viewport, picked);
        var size = _styleService.MarkerSize * Magnification;
        return new Rect(center.X - size / 2, center.Y - size / 2, size, size);
    }

    private static Point ToScreen(MapViewport viewport, MapSelection picked)
    {
        var (x, y) = viewport.ToScreen(picked.Position.X, picked.Position.Z);
        return new Point(x, y);
    }

    private static Rect ToScreen(MapScene scene, BlockBox box)
    {
        var (left, top) = scene.Viewport.ToScreen(box.MinX, box.MinZ);
        var (right, bottom) = scene.Viewport.ToScreen(box.MaxX + 1.0, box.MaxZ + 1.0);
        return new Rect(new Point(left, top), new Point(right, bottom));
    }
}
