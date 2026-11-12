using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// Points spread at random over an area, from a SplitMix64 sequence, so that a seed and a condition give the same
/// points on every platform.
/// </summary>
internal sealed class RandomPoints
{
    private const ulong Increment = 0x9E3779B97F4A7C15;
    private const ulong FirstMultiplier = 0xBF58476D1CE4E5B9;
    private const ulong SecondMultiplier = 0x94D049BB133111EB;

    private ulong _state;

    public RandomPoints(long seed, int conditionId)
    {
        _state = unchecked((ulong)seed ^ ((ulong)conditionId * Increment));
    }

    /// <summary>The next point of the area around a reference; points of a circle's rectangle that miss the circle are skipped.</summary>
    public BlockPos Next(SearchArea area, BlockPos reference)
    {
        while (true)
        {
            var position = new BlockPos(reference.X + area.MinX + NextBelow(area.SizeX), reference.Z + area.MinZ + NextBelow(area.SizeZ));
            if (area.Contains(reference, position))
                return position;
        }
    }

    private int NextBelow(int bound) => (int)(NextValue() % (ulong)bound);

    private ulong NextValue()
    {
        unchecked
        {
            var value = _state += Increment;
            value = (value ^ (value >> 30)) * FirstMultiplier;
            value = (value ^ (value >> 27)) * SecondMultiplier;
            return value ^ (value >> 31);
        }
    }
}
