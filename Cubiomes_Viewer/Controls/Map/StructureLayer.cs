using System.Collections.Frozen;

using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Marks the structures of the chosen types, each type only while it is not too dense for the zoom.</summary>
public sealed class StructureLayer : IMarkerLayer
{
    private static readonly RenderOptions _markerRenderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>The structure types to mark.</summary>
    public IReadOnlySet<StructureType> VisibleTypes { get; set; } = FrozenSet<StructureType>.Empty;

    /// <summary>How far out the types are marked where it differs from their defaults, in blocks per pixel.</summary>
    public IReadOnlyDictionary<StructureType, double> ZoomLimits { get; set; } = FrozenDictionary<StructureType, double>.Empty;

    /// <summary>Per structure type, the bits of the feature options whose structures are left out.</summary>
    public IReadOnlyDictionary<StructureType, ulong> HiddenOptions { get; set; } = FrozenDictionary<StructureType, ulong>.Empty;

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
                var hidden = HiddenOptions.GetValueOrDefault(request.Type);
                for (var index = 0; index < structures.Positions.Length; index++)
                {
                    if (IsHidden(structures, index, hidden))
                        continue;

                    var position = structures.Positions[index];
                    var (x, y) = scene.Viewport.ToScreen(position.X, position.Z);
                    var center = new Point(scene.Snap(x), scene.Snap(y));
                    if (bounds.Contains(center))
                        _styleService.DrawMarker(context, marker, center);
                }
            }
        }
    }

    public MapSelection? HitTest(MapScene scene, Point position)
    {
        if (!IsVisible)
            return null;

        var reach = _styleService.MarkerSize / 2;
        for (var index = _requests.Count - 1; index >= 0; index--)
        {
            var request = _requests[index];
            if (!_areaService.TryGetStructures(request, out var structures))
                continue;

            var hidden = HiddenOptions.GetValueOrDefault(request.Type);
            for (var last = structures.Positions.Length - 1; last >= 0; last--)
            {
                if (IsHidden(structures, last, hidden))
                    continue;

                var structure = structures.Positions[last];
                var (x, y) = scene.Viewport.ToScreen(structure.X, structure.Z);
                if (Math.Abs(x - position.X) <= reach && Math.Abs(y - position.Y) <= reach)
                    return new MapSelection(_catalog.GetInfo(request.Type).Name, _styleService.GetStructureMarker(request.Type), structure, request.Type);
            }
        }

        return null;
    }

    /// <summary>A structure has one option of each feature, so it is left out when any of its options is hidden.</summary>
    private static bool IsHidden(StructureAreaData structures, int index, ulong hiddenOptions)
        => hiddenOptions != 0 && index < structures.Options.Length && (structures.Options[index] & hiddenOptions) != 0;

    private List<StructureAreaRequest> GetRequests(MapScene scene)
    {
        List<StructureAreaRequest> requests = [];
        foreach (var structure in _catalog.Structures)
        {
            if (VisibleTypes.Contains(structure.Type) && scene.Viewport.BlocksPerPixel <= ZoomLimits.GetValueOrDefault(structure.Type, structure.MaxBlocksPerPixel))
                requests.AddRange(_areaService.GetVisibleAreas(scene.World, structure.Type, scene.Viewport));
        }

        return requests;
    }
}
