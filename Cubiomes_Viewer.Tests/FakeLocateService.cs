using Cubiomes_Viewer.Engine.Data.Locate;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LocateServ;

namespace Cubiomes_Viewer.Tests;

/// <summary>A search for the nearest place that finds nothing, either at once or only once it is cancelled.</summary>
internal sealed class FakeLocateService(bool waitsUntilCancelled) : ILocateService
{
    public const int StructureRange = 54_400;

    public int BiomeRange => 6_400;

    public int GetStructureRange(StructureType type, McVersion version) => StructureRange;

    public Task<LocateResult?> FindStructureAsync(WorldKey world, StructureType type, BlockPos origin, int range, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        => FindNothingAsync(cancellationToken);

    public Task<LocateResult?> FindBiomeAsync(WorldKey world, int biome, int y, BlockPos origin, int range, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        => FindNothingAsync(cancellationToken);

    public Task<LocateResult?> FindStrongholdAsync(WorldKey world, BlockPos origin, CancellationToken cancellationToken = default)
        => FindNothingAsync(cancellationToken);

    private async Task<LocateResult?> FindNothingAsync(CancellationToken cancellationToken)
    {
        if (waitsUntilCancelled)
            await Task.Delay(Timeout.Infinite, cancellationToken);

        return null;
    }
}
