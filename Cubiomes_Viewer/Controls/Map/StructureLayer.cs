using System.Collections.Frozen;

using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Marks the structures of the chosen types, each type up to the zoom it is marked to. Structures whose markers would overlap
/// are drawn as one marker with their number, which a click zooms in on.
/// </summary>
public sealed class StructureLayer : IMarkerLayer
{
    private const int ReachStepsPerDoubling = 4;
    private static readonly RenderOptions _markerRenderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>The structure types to mark.</summary>
    public IReadOnlySet<StructureType> VisibleTypes { get; set; } = FrozenSet<StructureType>.Empty;

    /// <summary>How far out the types are marked where it differs from their defaults, in blocks per pixel.</summary>
    public IReadOnlyDictionary<StructureType, double> ZoomLimits { get; set; } = FrozenDictionary<StructureType, double>.Empty;

    /// <summary>Per structure type, the bits of the feature options whose structures are left out.</summary>
    public IReadOnlyDictionary<StructureType, ulong> HiddenOptions
    {
        get;
        set
        {
            field = value;
            _groups = null;
        }
    } = FrozenDictionary<StructureType, ulong>.Empty;

    private readonly IStructureAreaService _areaService;
    private readonly IStructureCatalog _catalog;
    private readonly IMapStyleService _styleService;
    private readonly UiThreadCoalescer _invalidation;
    private IReadOnlyList<StructureAreaRequest> _requests = [];
    private TypeGroups[]? _groups;
    private double _groupedReach;

    public StructureLayer(IStructureAreaService areaService, IStructureCatalog catalog, IMapStyleService styleService)
    {
        _areaService = areaService;
        _catalog = catalog;
        _styleService = styleService;
        _invalidation = new UiThreadCoalescer(OnAreasChanged);
    }

    public void Attach() => _areaService.StateChanged += _invalidation.Request;

    public void Detach()
    {
        _areaService.StateChanged -= _invalidation.Request;
        _requests = [];
        _groups = null;
        _areaService.SetVisibleAreas(_requests);
    }

    public void UpdateScene(MapScene scene)
    {
        var requests = IsVisible && !scene.Viewport.IsEmpty ? GetRequests(scene) : [];
        if (!requests.SequenceEqual(_requests))
            _groups = null;

        _requests = requests;
        _areaService.SetVisibleAreas(_requests);
    }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || _requests.Count == 0)
            return;

        var half = _styleService.GetMarkerSize(scene.Pixels) / 2;
        var bounds = new Rect(-half, -half, scene.Viewport.Width + 2 * half, scene.Viewport.Height + 2 * half);
        using (context.PushRenderOptions(_markerRenderOptions))
        {
            foreach (var type in GetGroups(scene))
            {
                var marker = _styleService.GetStructureMarker(type.Type);
                foreach (var group in type.Groups)
                {
                    var center = ToScreen(scene, group);
                    if (!bounds.Contains(center))
                        continue;

                    _styleService.DrawMarker(context, marker, center, scene.Pixels);
                    if (!group.IsSingle)
                        _styleService.DrawCount(context, group.Count, new Point(center.X + half, center.Y - half));
                }
            }
        }
    }

    public MapSelection? HitTest(MapScene scene, Point position)
    {
        if (FindAt(scene, position) is not { Group.IsSingle: true } hit)
            return null;

        var (type, structure) = (hit.Type.Type, hit.Type.Positions[hit.Group.First]);
        return MapSelection.ForStructure(_catalog.GetInfo(type).Name, _styleService.GetStructureMarker(type), structure, type);
    }

    public MarkerGroup? HitTestGroup(MapScene scene, Point position)
        => FindAt(scene, position) is { Group.IsSingle: false } hit ? hit.Group : null;

    /// <summary>The topmost marker at a position: the last one drawn there.</summary>
    private Hit? FindAt(MapScene scene, Point position)
    {
        if (!IsVisible || _requests.Count == 0)
            return null;

        var reach = _styleService.GetMarkerSize(scene.Pixels) / 2;
        var types = GetGroups(scene);
        for (var typeIndex = types.Length - 1; typeIndex >= 0; typeIndex--)
        {
            var groups = types[typeIndex].Groups;
            for (var index = groups.Length - 1; index >= 0; index--)
            {
                var center = ToScreen(scene, groups[index]);
                if (Math.Abs(center.X - position.X) <= reach && Math.Abs(center.Y - position.Y) <= reach)
                    return new Hit(types[typeIndex], groups[index]);
            }
        }

        return null;
    }

    /// <summary>
    /// The markers of the loaded structures, grouped where they would overlap at the zoom of the scene. The reach is rounded to a
    /// quarter of a zoom step, so that a gliding zoom groups them anew a few times instead of in every frame; the groups are kept
    /// until the reach or the structures change.
    /// </summary>
    private TypeGroups[] GetGroups(MapScene scene)
    {
        var exact = _styleService.GetMarkerSize(scene.Pixels) * scene.Viewport.BlocksPerPixel;
        var reach = Math.Pow(2, Math.Round(Math.Log2(exact) * ReachStepsPerDoubling) / ReachStepsPerDoubling);
        if (_groups is { } groups && _groupedReach.Equals(reach))
            return groups;

        _groupedReach = reach;
        return _groups = [.. _requests.GroupBy(request => request.Type).Select(requests => GroupType(requests.Key, requests, reach))];
    }

    private TypeGroups GroupType(StructureType type, IEnumerable<StructureAreaRequest> requests, double reach)
    {
        var hidden = HiddenOptions.GetValueOrDefault(type);
        List<BlockPos> positions = [];
        foreach (var request in requests)
        {
            if (!_areaService.TryGetStructures(request, out var structures))
                continue;

            for (var index = 0; index < structures.Positions.Length; index++)
            {
                if (!IsHidden(structures, index, hidden))
                    positions.Add(structures.Positions[index]);
            }
        }

        return new TypeGroups(type, positions, MarkerGrouping.Group(positions, reach));
    }

    private void OnAreasChanged()
    {
        _groups = null;
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private static Point ToScreen(MapScene scene, MarkerGroup group)
    {
        var (x, y) = scene.Viewport.ToScreen(group.X, group.Z);
        return new Point(x, y);
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

    private sealed record TypeGroups(StructureType Type, IReadOnlyList<BlockPos> Positions, MarkerGroup[] Groups);

    private readonly record struct Hit(TypeGroups Type, MarkerGroup Group);
}
