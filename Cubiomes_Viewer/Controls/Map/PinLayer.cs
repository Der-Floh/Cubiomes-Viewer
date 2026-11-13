using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Service.PinServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Draws the pins put on the map of the open dimension of the seed.</summary>
public sealed class PinLayer : IMarkerLayer
{
    private const double BlockCenter = 0.5;
    private static readonly RenderOptions _renderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    private readonly IPinService _pinService;
    private readonly IMapStyleService _styleService;

    public PinLayer(IPinService pinService, IMapStyleService styleService)
    {
        _pinService = pinService;
        _styleService = styleService;
    }

    /// <summary>Where the tip of a pin is drawn: in the middle of its block, so that it points at the block at every zoom.</summary>
    public static Point GetTip(MapViewport viewport, MapPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        var (x, y) = viewport.ToScreen(pin.X + BlockCenter, pin.Z + BlockCenter);
        return new Point(x, y);
    }

    public void Attach() => _pinService.Changed += OnPinsChanged;
    public void Detach() => _pinService.Changed -= OnPinsChanged;
    public void UpdateScene(MapScene scene) { }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
            return;

        var view = new Rect(0, 0, scene.Viewport.Width, scene.Viewport.Height);
        using (context.PushRenderOptions(_renderOptions))
        {
            foreach (var pin in _pinService.GetPins(scene.World.Seed, scene.World.Dimension))
            {
                var tip = GetTip(scene.Viewport, pin);
                if (_styleService.GetPinBounds(tip).Intersects(view))
                    _styleService.DrawPin(context, _styleService.GetPinLook(pin.Icon), tip, scene.Pixels);
            }
        }
    }

    public MapSelection? HitTest(MapScene scene, Point position)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
            return null;

        var pins = _pinService.GetPins(scene.World.Seed, scene.World.Dimension);
        for (var index = pins.Count - 1; index >= 0; index--)
        {
            if (_styleService.GetPinBounds(GetTip(scene.Viewport, pins[index])).Contains(position))
                return MapSelection.ForPin(pins[index]);
        }

        return null;
    }

    private void OnPinsChanged() => Invalidated?.Invoke(this, EventArgs.Empty);
}
