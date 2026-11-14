using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Locate;

/// <summary>The nearest place a search found and how far it lies from where the search began; <see cref="Index"/> numbers a stronghold in generation order.</summary>
public sealed record LocateResult(BlockPos Position, long SquaredDistance, int? Index = null)
{
    public double Distance => Math.Sqrt(SquaredDistance);
}
