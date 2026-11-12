using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>
/// Structure seeds in ascending order, each with its sister seeds in the order of their upper 16 bits. A batch may hold
/// the sister seeds of many structure seeds, since the worker rules out all of them with one quick look.
/// </summary>
public sealed class StructureSeedSequence : ISeedSequence
{
    public UInt128 Count { get; }
    public bool IsEndless { get; }
    public int MaxBatchCount => int.MaxValue;

    private readonly long _firstStructureSeed;

    public StructureSeedSequence(StructureSeedSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(source.FirstStructureSeed);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(source.LastStructureSeed, StructureSeed.Max);
        ArgumentOutOfRangeException.ThrowIfLessThan(source.LastStructureSeed, source.FirstStructureSeed);

        _firstStructureSeed = source.FirstStructureSeed;
        Count = (UInt128)(source.LastStructureSeed - source.FirstStructureSeed + 1) * StructureSeed.SisterSeeds;
        IsEndless = source.LastStructureSeed == StructureSeed.Max;
    }

    public long GetSeed(UInt128 position)
    {
        var (structureSeed, upperBits) = Split(position);
        return StructureSeed.ToWorldSeed(structureSeed, upperBits);
    }

    public SearchJob CreateBatch(SearchDefinition definition, UInt128 position, int maxCount)
    {
        var (structureSeed, upperBits) = Split(position);
        return new StructureSeedBatchJob(definition, structureSeed, upperBits, (int)UInt128.Min((UInt128)maxCount, Count - position));
    }

    private (long StructureSeed, int UpperBits) Split(UInt128 position)
        => (_firstStructureSeed + (long)(position / StructureSeed.SisterSeeds), (int)(position % StructureSeed.SisterSeeds));
}
