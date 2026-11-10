using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class EndGatewayService : IEndGatewayService
{
    private const int CachedWorlds = 16;

    public event Action? StateChanged
    {
        add => _loader.StateChanged += value;
        remove => _loader.StateChanged -= value;
    }

    private readonly VisibleSetLoader<WorldKey, EndGatewayData> _loader;

    public EndGatewayService(IComputeService computeService, ILogger<EndGatewayService> logger)
    {
        var cache = new LruCache<WorldKey, EndGatewayData>(CachedWorlds, static _ => 1);
        _loader = new VisibleSetLoader<WorldKey, EndGatewayData>(
            computeService, cache, static world => new EndGatewaysJob(world), JobPriorityClass.Background, logger);
    }

    public void Show(WorldKey world) => _loader.SetVisible([ToEnd(world)]);

    public bool TryGetGateways(WorldKey world, [NotNullWhen(true)] out EndGatewayData? gateways)
        => _loader.TryGet(ToEnd(world), out gateways);

    private static WorldKey ToEnd(WorldKey world) => world with { Dimension = Dimension.End };
}
