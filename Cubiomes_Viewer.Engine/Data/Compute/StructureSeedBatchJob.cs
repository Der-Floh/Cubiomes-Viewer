using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>
/// Tests <see cref="Count"/> seeds that run through the sister seeds of consecutive structure seeds, from the sister seed
/// of <see cref="FirstStructureSeed"/> with the upper 16 bits <see cref="FirstUpperBits"/> on.
/// </summary>
public sealed record StructureSeedBatchJob(SearchDefinition Definition, long FirstStructureSeed, int FirstUpperBits, int Count) : SearchJob
{
    /// <summary>The structure seed of a seed of the batch and the upper 16 bits that make it that seed.</summary>
    public (long StructureSeed, int UpperBits) Locate(int index)
    {
        var position = (long)FirstUpperBits + index;
        return (FirstStructureSeed + position / StructureSeed.SisterSeeds, (int)(position % StructureSeed.SisterSeeds));
    }

    public long GetSeed(int index)
    {
        var (structureSeed, upperBits) = Locate(index);
        return StructureSeed.ToWorldSeed(structureSeed, upperBits);
    }
}
