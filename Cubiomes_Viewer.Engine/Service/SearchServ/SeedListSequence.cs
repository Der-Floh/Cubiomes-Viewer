using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>The seeds of a list in its order; each batch carries its seeds to the worker.</summary>
public sealed class SeedListSequence : ISeedSequence
{
    public UInt128 Count => (UInt128)_seeds.Length;
    public bool IsEndless => false;
    public int MaxBatchCount => ISeedSequence.DefaultMaxBatchCount;

    private readonly long[] _seeds;

    public SeedListSequence(SeedListSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _seeds = [.. source.Seeds];
    }

    public long GetSeed(UInt128 position) => _seeds[(int)position];

    public SearchJob CreateBatch(SearchDefinition definition, UInt128 position, int maxCount)
    {
        var start = (int)position;
        long[] seeds = _seeds[start..Math.Min(_seeds.Length, start + maxCount)];
        return new SearchBatchJob(definition, seeds[0], seeds.Length, Seeds: seeds);
    }
}
