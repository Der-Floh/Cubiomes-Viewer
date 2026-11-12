using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>Consecutive seeds from a first to a last one.</summary>
public sealed class SeedRangeSequence : ISeedSequence
{
    public UInt128 Count { get; }
    public bool IsEndless { get; }

    private readonly long _firstSeed;

    public SeedRangeSequence(SeedRangeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(source.LastSeed, source.FirstSeed);

        _firstSeed = source.FirstSeed;
        Count = (UInt128)unchecked((ulong)source.LastSeed - (ulong)source.FirstSeed) + 1;
        IsEndless = source.LastSeed == long.MaxValue;
    }

    public long GetSeed(UInt128 position) => unchecked(_firstSeed + (long)(ulong)position);

    public SearchBatchJob CreateBatch(SearchDefinition definition, UInt128 position, int maxCount)
        => new(definition, GetSeed(position), (int)UInt128.Min((UInt128)maxCount, Count - position));
}
