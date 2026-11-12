using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>
/// A batch of seeds to test against a search definition. The worker tests them in order and may stop early, after a
/// seed that brings the matches it needs or once its time is up; its result tells how many seeds it tested.
/// </summary>
public abstract record SearchJob : ComputeJob<SearchBatchResult>
{
    /// <summary>The number of seeds in the batch.</summary>
    public abstract int Count { get; init; }

    /// <summary>How many matches the search still needs.</summary>
    public int MaxMatches { get; init; } = int.MaxValue;

    /// <summary>How long the worker may test seeds; it always tests the first one.</summary>
    public int MaxMilliseconds { get; init; } = int.MaxValue;
}
