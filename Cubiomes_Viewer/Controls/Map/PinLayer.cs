using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.PinServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Draws the pins put on the map of the open dimension of the seed. Pins whose heads would overlap are drawn as one pin with
/// their number, which a click zooms in on.
/// </summary>
public sealed class PinLayer : IMarkerLayer
{
    private const double BlockCenter = 0.5;

    /// <summary>How close two pins may come, as a share of their size, before their heads overlap.</summary>
    private const double HeadShare = 0.6;

    /// <summary>Where the number of a group sits on the pin's head, as shares of the pin's size from its top right corner.</summary>
    private const double CountInset = 0.2;

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

        return GetTip(viewport, pin.X, pin.Z);
    }

    public void Attach() => _pinService.Changed += OnPinsChanged;
    public void Detach() => _pinService.Changed -= OnPinsChanged;
    public void UpdateScene(MapScene scene) { }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
            return;

        var view = new Rect(0, 0, scene.Viewport.Width, scene.Viewport.Height);
        var (pins, groups) = GetGroups(scene);
        using (context.PushRenderOptions(_renderOptions))
        {
            foreach (var group in groups)
            {
                var tip = GetTip(scene.Viewport, group.X, group.Z);
                var bounds = _styleService.GetPinBounds(tip);
                if (!bounds.Intersects(view))
                    continue;

                _styleService.DrawPin(context, _styleService.GetPinLook(pins[group.First].Icon), tip, scene.Pixels);
                if (!group.IsSingle)
                    _styleService.DrawCount(context, group.Count, new Point(bounds.Right - bounds.Width * CountInset, bounds.Top + bounds.Height * CountInset));
            }
        }
    }

    public MapSelection? HitTest(MapScene scene, Point position)
    {
        if (FindAt(scene, position) is not { Group.IsSingle: true } hit)
            return null;

        return MapSelection.ForPin(hit.Pin);
    }

    public MarkerGroup? HitTestGroup(MapScene scene, Point position)
        => FindAt(scene, position) is { Group.IsSingle: false } hit ? hit.Group : null;

    /// <summary>The topmost pin or group at a position: the last one drawn there.</summary>
    private Hit? FindAt(MapScene scene, Point position)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
            return null;

        var (pins, groups) = GetGroups(scene);
        for (var index = groups.Length - 1; index >= 0; index--)
        {
            var group = groups[index];
            if (_styleService.GetPinBounds(GetTip(scene.Viewport, group.X, group.Z)).Contains(position))
                return new Hit(pins[group.First], group);
        }

        return null;
    }

    private (IReadOnlyList<MapPin> Pins, MarkerGroup[] Groups) GetGroups(MapScene scene)
    {
        var pins = _pinService.GetPins(scene.World.Seed, scene.World.Dimension);
        var reach = _styleService.PinSize * HeadShare * scene.Viewport.BlocksPerPixel;
        return (pins, MarkerGrouping.Group([.. pins.Select(pin => new BlockPos(pin.X, pin.Z))], reach));
    }

    private static Point GetTip(MapViewport viewport, double blockX, double blockZ)
    {
        var (x, y) = viewport.ToScreen(blockX + BlockCenter, blockZ + BlockCenter);
        return new Point(x, y);
    }

    private void OnPinsChanged() => Invalidated?.Invoke(this, EventArgs.Empty);

    private readonly record struct Hit(MapPin Pin, MarkerGroup Group);
}
