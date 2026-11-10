using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Marks the spawn point and the strongholds of the Overworld.</summary>
public sealed class LandmarkLayer : IMapLayer
{
    private static readonly RenderOptions _markerRenderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;
    public bool ShowSpawn { get; set; } = true;
    public bool ShowStrongholds { get; set; } = true;

    private readonly IWorldLandmarkService _landmarkService;
    private readonly IMapStyleService _styleService;
    private readonly UiThreadCoalescer _invalidation;
    private WorldKey? _shownWorld;

    public LandmarkLayer(IWorldLandmarkService landmarkService, IMapStyleService styleService)
    {
        _landmarkService = landmarkService;
        _styleService = styleService;
        _invalidation = new UiThreadCoalescer(() => Invalidated?.Invoke(this, EventArgs.Empty));
    }

    public void Attach() => _landmarkService.StateChanged += _invalidation.Request;

    public void Detach()
    {
        _landmarkService.StateChanged -= _invalidation.Request;
        _shownWorld = null;
    }

    public void UpdateScene(MapScene scene)
    {
        if (!HasContent(scene) || _shownWorld == scene.World)
            return;

        _shownWorld = scene.World;
        _landmarkService.Show(scene.World);
    }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!HasContent(scene) || !_landmarkService.TryGetLandmarks(scene.World, out var landmarks))
            return;

        using (context.PushRenderOptions(_markerRenderOptions))
        {
            if (ShowStrongholds)
            {
                foreach (var stronghold in landmarks.Strongholds)
                    DrawMarker(context, scene, _styleService.Stronghold, stronghold);
            }

            if (ShowSpawn && landmarks.Spawn is { } spawn)
                DrawMarker(context, scene, _styleService.Spawn, spawn);
        }
    }

    private bool HasContent(MapScene scene)
        => IsVisible && (ShowSpawn || ShowStrongholds) && scene.World.Dimension == Dimension.Overworld && !scene.Viewport.IsEmpty;

    private void DrawMarker(DrawingContext context, MapScene scene, MapMarker marker, BlockPos position)
    {
        var margin = _styleService.MarkerSize / 2;
        var (x, y) = scene.Viewport.ToScreen(position.X, position.Z);
        if (x < -margin || y < -margin || x > scene.Viewport.Width + margin || y > scene.Viewport.Height + margin)
            return;

        _styleService.DrawMarker(context, marker, new Point(scene.Snap(x), scene.Snap(y)));
    }
}
