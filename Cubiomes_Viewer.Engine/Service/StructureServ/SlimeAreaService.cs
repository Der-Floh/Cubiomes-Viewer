using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class SlimeAreaService : ISlimeAreaService
{
    public const int ChunkBlocks = 16;

    private const int AreaChunks = 128;
    private const int MaxVisibleAreas = 64;
    private const long CacheBudgetBytes = 8L * 1024 * 1024;

    public event Action? StateChanged
    {
        add => _loader.StateChanged += value;
        remove => _loader.StateChanged -= value;
    }

    private readonly VisibleSetLoader<SlimeAreaRequest, SlimeAreaData> _loader;

    public SlimeAreaService(IComputeService computeService, ILogger<SlimeAreaService> logger)
    {
        var cache = new LruCache<SlimeAreaRequest, SlimeAreaData>(CacheBudgetBytes, static area => area.Chunks.Length);
        _loader = new VisibleSetLoader<SlimeAreaRequest, SlimeAreaData>(
            computeService, cache, static request => new SlimeAreaJob(request), JobPriorityClass.Visible, logger);
    }

    public IReadOnlyList<SlimeAreaRequest> GetVisibleAreas(long seed, MapViewport viewport)
        => [.. SquareGrid.GetVisibleCells(AreaChunks * ChunkBlocks, viewport, MaxVisibleAreas)
            .Select(area => new SlimeAreaRequest(seed, area.X / ChunkBlocks, area.Z / ChunkBlocks, AreaChunks))];

    public void SetVisibleAreas(IReadOnlyList<SlimeAreaRequest> requests) => _loader.SetVisible(requests);

    public bool TryGetSlimeChunks(SlimeAreaRequest request, [NotNullWhen(true)] out SlimeAreaData? slimeChunks)
        => _loader.TryGet(request, out slimeChunks);
}
