using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureAreaService : IStructureAreaService
{
    private const int RegionsPerAreaSide = 16;
    private const int MinAreaBlocks = 1024;
    private const int MaxVisibleAreas = 256;
    private const int CachedPositionBudget = 500_000;
    private const int AreaOverhead = 16;

    public event Action? StateChanged
    {
        add => _loader.StateChanged += value;
        remove => _loader.StateChanged -= value;
    }

    public int PendingCount => _loader.PendingCount;

    private readonly IStructureFinder _structureFinder;
    private readonly VisibleSetLoader<StructureAreaRequest, StructureAreaData> _loader;

    public StructureAreaService(IComputeService computeService, IStructureFinder structureFinder, ILogger<StructureAreaService> logger)
    {
        _structureFinder = structureFinder;

        var cache = new LruCache<StructureAreaRequest, StructureAreaData>(CachedPositionBudget, static area => area.Positions.Length + AreaOverhead);
        _loader = new VisibleSetLoader<StructureAreaRequest, StructureAreaData>(
            computeService, cache, static request => new StructureAreaJob(request), JobPriorityClass.Visible, logger);
    }

    public IReadOnlyList<StructureAreaRequest> GetVisibleAreas(WorldKey world, StructureType type, MapViewport viewport)
    {
        if (!_structureFinder.TryGetPlacement(type, world.Version, out var placement) || placement.Dimension != world.Dimension)
            return [];

        var areaBlocks = Math.Max(placement.RegionBlocks * RegionsPerAreaSide, MinAreaBlocks);
        return [.. SquareGrid.GetVisibleCells(areaBlocks, viewport, MaxVisibleAreas).Select(area => new StructureAreaRequest(world, type, area))];
    }

    public void SetVisibleAreas(IReadOnlyList<StructureAreaRequest> requests) => _loader.SetVisible(requests);

    public bool TryGetStructures(StructureAreaRequest request, [NotNullWhen(true)] out StructureAreaData? structures)
        => _loader.TryGet(request, out structures);
}
