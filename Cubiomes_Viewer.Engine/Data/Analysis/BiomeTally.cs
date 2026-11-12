using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>How many samples of an area have one biome, and which of them lies nearest to the reference point.</summary>
public readonly record struct BiomeTally(int Biome, int Samples, BlockPos Nearest, long NearestSquaredDistance)
{
    public double NearestDistance => Math.Sqrt(NearestSquaredDistance);
}
