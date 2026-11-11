using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Marks the gateways around the main End island and draws a line from each to the place it leads to.</summary>
public sealed class GatewayLayer : IMarkerLayer
{
    private const string GatewayName = "End gateway";
    private const string ExitName = "Gateway exit";
    private const double LinkThickness = 1.5;
    private static readonly RenderOptions _renderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    private readonly IEndGatewayService _gatewayService;
    private readonly IMapStyleService _styleService;
    private readonly UiThreadCoalescer _invalidation;
    private WorldKey? _shownWorld;

    private MapMarker Marker => _styleService.GetStructureMarker(StructureType.EndGateway);
    private IPen LinkPen => field ??= new ImmutablePen(Marker.Fill.ToImmutable(), LinkThickness);

    public GatewayLayer(IEndGatewayService gatewayService, IMapStyleService styleService)
    {
        _gatewayService = gatewayService;
        _styleService = styleService;
        _invalidation = new UiThreadCoalescer(() => Invalidated?.Invoke(this, EventArgs.Empty));
    }

    public void Attach() => _gatewayService.StateChanged += _invalidation.Request;

    public void Detach()
    {
        _gatewayService.StateChanged -= _invalidation.Request;
        _shownWorld = null;
    }

    public void UpdateScene(MapScene scene)
    {
        if (!HasContent(scene) || _shownWorld == scene.World)
            return;

        _shownWorld = scene.World;
        _gatewayService.Show(scene.World);
    }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!HasContent(scene) || !_gatewayService.TryGetGateways(scene.World, out var gateways))
            return;

        using (context.PushRenderOptions(_renderOptions))
        {
            foreach (var gateway in gateways.Gateways)
                context.DrawLine(LinkPen, ToScreen(scene, gateway.Position), ToScreen(scene, gateway.Destination));

            foreach (var gateway in gateways.Gateways)
            {
                _styleService.DrawMarker(context, Marker, ToScreen(scene, gateway.Position));
                _styleService.DrawMarker(context, Marker, ToScreen(scene, gateway.Destination));
            }
        }
    }

    public MapSelection? HitTest(MapScene scene, Point position)
    {
        if (!HasContent(scene) || !_gatewayService.TryGetGateways(scene.World, out var gateways))
            return null;

        for (var index = gateways.Gateways.Length - 1; index >= 0; index--)
        {
            var gateway = gateways.Gateways[index];
            if (IsHit(scene, gateway.Destination, position))
                return new MapSelection($"{ExitName} {index + 1}", Marker, gateway.Destination);
            if (IsHit(scene, gateway.Position, position))
                return new MapSelection($"{GatewayName} {index + 1}", Marker, gateway.Position);
        }

        return null;
    }

    private bool HasContent(MapScene scene)
        => IsVisible && scene.World.Dimension == Dimension.End && !scene.Viewport.IsEmpty;

    private bool IsHit(MapScene scene, BlockPos marker, Point position)
    {
        var reach = _styleService.MarkerSize / 2;
        var center = ToScreen(scene, marker);
        return Math.Abs(center.X - position.X) <= reach && Math.Abs(center.Y - position.Y) <= reach;
    }

    private static Point ToScreen(MapScene scene, BlockPos block)
    {
        var (x, y) = scene.Viewport.ToScreen(block.X, block.Z);
        return new Point(scene.Snap(x), scene.Snap(y));
    }
}
