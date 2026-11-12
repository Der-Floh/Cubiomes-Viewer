using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>
/// The lower 48 bits of seeds with a quad, each followed by every value of the upper 16 bits. One batch never leaves
/// one lower part, so it is a run of seeds 2^48 apart.
/// </summary>
public sealed class QuadHutSeedSequence : ISeedSequence
{
    private const int UpperBits = 16;
    private const int UpperShift = 64 - UpperBits;
    private const long UpperStride = 1L << UpperShift;
    private const int UppersPerLower = 1 << UpperBits;

    public UInt128 Count { get; }
    public bool IsEndless => false;

    private readonly long[] _lowerSeeds;

    public QuadHutSeedSequence(long[] lowerSeeds)
    {
        ArgumentNullException.ThrowIfNull(lowerSeeds);

        _lowerSeeds = lowerSeeds;
        Count = (UInt128)lowerSeeds.Length * UppersPerLower;
    }

    public long GetSeed(UInt128 position)
    {
        var (lower, upper) = Split(position);
        return _lowerSeeds[lower] | (upper << UpperShift);
    }

    public SearchBatchJob CreateBatch(SearchDefinition definition, UInt128 position, int maxCount)
    {
        var (_, upper) = Split(position);
        return new SearchBatchJob(definition, GetSeed(position), (int)Math.Min(maxCount, UppersPerLower - upper), UpperStride);
    }

    private static (int Lower, long Upper) Split(UInt128 position) => ((int)(position / UppersPerLower), (long)(position % UppersPerLower));
}
