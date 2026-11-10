using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class WorldLandmarkService : IWorldLandmarkService
{
    private const int CachedWorlds = 16;

    public event Action? StateChanged
    {
        add => _loader.StateChanged += value;
        remove => _loader.StateChanged -= value;
    }

    private readonly VisibleSetLoader<WorldKey, WorldLandmarks> _loader;

    public WorldLandmarkService(IComputeService computeService, ILogger<WorldLandmarkService> logger)
    {
        var cache = new LruCache<WorldKey, WorldLandmarks>(CachedWorlds, static _ => 1);
        _loader = new VisibleSetLoader<WorldKey, WorldLandmarks>(
            computeService, cache, static world => new LandmarksJob(world), JobPriorityClass.Background, logger);
    }

    public void Show(WorldKey world) => _loader.SetVisible([ToOverworld(world)]);

    public bool TryGetLandmarks(WorldKey world, [NotNullWhen(true)] out WorldLandmarks? landmarks)
        => _loader.TryGet(ToOverworld(world), out landmarks);

    private static WorldKey ToOverworld(WorldKey world) => world with { Dimension = Dimension.Overworld };
}
