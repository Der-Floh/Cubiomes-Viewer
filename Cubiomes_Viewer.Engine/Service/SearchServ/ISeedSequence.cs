using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>The seeds of a source in the order a search scans them, numbered by their position from 0 on.</summary>
public interface ISeedSequence
{
    UInt128 Count { get; }

    /// <summary>Whether the sequence runs to the end of the 64-bit seeds, where a time left means nothing.</summary>
    bool IsEndless { get; }

    long GetSeed(UInt128 position);

    /// <summary>A batch of at most <paramref name="maxCount"/> seeds from a position on; it may be shorter, to keep a run of seeds simple to describe.</summary>
    SearchBatchJob CreateBatch(SearchDefinition definition, UInt128 position, int maxCount);
}
