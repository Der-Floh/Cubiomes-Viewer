using System.Collections.Frozen;

using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Marks the structures of the chosen types, each type only while it is not too dense for the zoom.</summary>
public sealed class StructureLayer : IMapLayer
{
    private static readonly RenderOptions _markerRenderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>The structure types to mark.</summary>
    public IReadOnlySet<StructureType> VisibleTypes { get; set; } = FrozenSet<StructureType>.Empty;

    private readonly IStructureAreaService _areaService;
    private readonly IStructureCatalog _catalog;
    private readonly IMapStyleService _styleService;
    private readonly UiThreadCoalescer _invalidation;
    private IReadOnlyList<StructureAreaRequest> _requests = [];

    public StructureLayer(IStructureAreaService areaService, IStructureCatalog catalog, IMapStyleService styleService)
    {
        _areaService = areaService;
        _catalog = catalog;
        _styleService = styleService;
        _invalidation = new UiThreadCoalescer(() => Invalidated?.Invoke(this, EventArgs.Empty));
    }

    public void Attach() => _areaService.StateChanged += _invalidation.Request;

    public void Detach()
    {
        _areaService.StateChanged -= _invalidation.Request;
        _requests = [];
        _areaService.SetVisibleAreas(_requests);
    }

    public void UpdateScene(MapScene scene)
    {
        _requests = IsVisible && !scene.Viewport.IsEmpty ? GetRequests(scene) : [];
        _areaService.SetVisibleAreas(_requests);
    }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || _requests.Count == 0)
            return;

        var margin = _styleService.MarkerSize / 2;
        var bounds = new Rect(-margin, -margin, scene.Viewport.Width + 2 * margin, scene.Viewport.Height + 2 * margin);

        using (context.PushRenderOptions(_markerRenderOptions))
        {
            foreach (var request in _requests)
            {
                if (!_areaService.TryGetStructures(request, out var structures))
                    continue;

                var marker = _styleService.GetStructureMarker(request.Type);
                foreach (var position in structures.Positions)
                {
                    var (x, y) = scene.Viewport.ToScreen(position.X, position.Z);
                    var center = new Point(scene.Snap(x), scene.Snap(y));
                    if (bounds.Contains(center))
                        _styleService.DrawMarker(context, marker, center);
                }
            }
        }
    }

    private List<StructureAreaRequest> GetRequests(MapScene scene)
    {
        List<StructureAreaRequest> requests = [];
        foreach (var structure in _catalog.Structures)
        {
            if (VisibleTypes.Contains(structure.Type) && scene.Viewport.BlocksPerPixel <= structure.MaxBlocksPerPixel)
                requests.AddRange(_areaService.GetVisibleAreas(scene.World, structure.Type, scene.Viewport));
        }

        return requests;
    }
}
