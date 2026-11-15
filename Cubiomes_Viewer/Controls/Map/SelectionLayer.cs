using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Draws the picked marker or pin larger than the others and above them, marks a picked spot with its pin, and outlines the boxes that
/// belong to the pick, such as the pieces of a structure.
/// </summary>
public sealed class SelectionLayer : IMapLayer
{
    /// <summary>How many times its normal size the picked marker or pin is drawn.</summary>
    public const double Magnification = 1.5;

    private const double BoxThickness = 1.5;
    private const byte BoxFillOpacity = 48;
    private const double BlockMiddle = 0.5;
    private static readonly RenderOptions _renderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>The picked marker, pin or spot, or <see langword="null"/> when nothing is picked.</summary>
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
    private readonly MeasurePainter _measurePainter;

    private ImmutableSolidColorBrush LineBrush => field ??= new ImmutableSolidColorBrush(_styleService.SelectionColor);
    private ImmutableSolidColorBrush FillBrush => field ??= new ImmutableSolidColorBrush(_styleService.SelectionColor, BoxFillOpacity / (double)byte.MaxValue);
    private IPen BoxPen => field ??= new ImmutablePen(LineBrush, BoxThickness);

    public SelectionLayer(IMapStyleService styleService, MeasurePainter measurePainter)
    {
        _styleService = styleService;
        _measurePainter = measurePainter;
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
            else if (picked.Measurement is { } measurement)
                _measurePainter.DrawKept(context, scene.Viewport, measurement, isPicked: true);
            else if (picked.SpotLook is { } look)
                _styleService.DrawPin(context, look, ToScreen(scene.Viewport, picked), scene.Pixels);
            else if (picked.Marker is { } marker)
                _styleService.DrawMarker(context, marker, ToScreen(scene.Viewport, picked), scene.Pixels, Magnification);
        }
    }

    /// <summary>
    /// Where a picked marker, pin or spot is drawn on a view of the map whose device pixels are <paramref name="pixels"/>; for a
    /// measurement, where its label is.
    /// </summary>
    public Rect GetMarkerBounds(MapViewport viewport, PixelGrid pixels, MapSelection picked)
    {
        ArgumentNullException.ThrowIfNull(picked);

        if (picked.Pin is { } pin)
            return _styleService.GetPinBounds(PinLayer.GetTip(viewport, pin), Magnification);
        if (picked.Measurement is { } measurement)
            return _measurePainter.GetLabelBounds(viewport, measurement);
        if (picked.IsSpot)
            return _styleService.GetPinBounds(ToScreen(viewport, picked));

        var center = ToScreen(viewport, picked);
        var size = _styleService.GetMarkerSize(pixels, Magnification);
        return new Rect(center.X - size / 2, center.Y - size / 2, size, size);
    }

    /// <summary>Where a pick lies on screen: a marker at its position, a spot in the middle of its block.</summary>
    private static Point ToScreen(MapViewport viewport, MapSelection picked)
    {
        var offset = picked.IsSpot ? BlockMiddle : 0;
        var (x, y) = viewport.ToScreen(picked.Position.X + offset, picked.Position.Z + offset);
        return new Point(x, y);
    }

    private static Rect ToScreen(MapScene scene, BlockBox box)
    {
        var (left, top) = scene.Viewport.ToScreen(box.MinX, box.MinZ);
        var (right, bottom) = scene.Viewport.ToScreen(box.MaxX + 1.0, box.MaxZ + 1.0);
        return new Rect(new Point(left, top), new Point(right, bottom));
    }
}
