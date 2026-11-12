using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>
/// The structure seeds with a quad, each followed by all of its sister seeds. One batch never leaves one structure
/// seed, so it is a run of seeds 2^48 apart.
/// </summary>
public sealed class QuadHutSeedSequence : ISeedSequence
{
    private const long UpperStride = 1L << StructureSeed.Bits;

    public UInt128 Count { get; }
    public bool IsEndless => false;
    public int MaxBatchCount => ISeedSequence.DefaultMaxBatchCount;

    private readonly long[] _structureSeeds;

    public QuadHutSeedSequence(long[] structureSeeds)
    {
        ArgumentNullException.ThrowIfNull(structureSeeds);

        _structureSeeds = structureSeeds;
        Count = (UInt128)structureSeeds.Length * StructureSeed.SisterSeeds;
    }

    public long GetSeed(UInt128 position)
    {
        var (index, upperBits) = Split(position);
        return StructureSeed.ToWorldSeed(_structureSeeds[index], upperBits);
    }

    public SearchJob CreateBatch(SearchDefinition definition, UInt128 position, int maxCount)
    {
        var (_, upperBits) = Split(position);
        return new SearchBatchJob(definition, GetSeed(position), Math.Min(maxCount, StructureSeed.SisterSeeds - upperBits), UpperStride);
    }

    private static (int Index, int UpperBits) Split(UInt128 position) => ((int)(position / StructureSeed.SisterSeeds), (int)(position % StructureSeed.SisterSeeds));
}
