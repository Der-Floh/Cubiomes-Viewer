namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>What a worker found in one batch of seeds.</summary>
public sealed class SearchBatchResult
{
    /// <summary>
    /// The number of seeds the worker tested, from the first of the batch on. A worker may stop before the end of a
    /// batch whose seeds take longer than expected; the search then hands the rest to another batch.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>Where in the batch the seeds that meet every condition are, ascending.</summary>
    public required int[] Matches { get; init; }

    /// <summary>How long the worker took, without the time the batch waited for it.</summary>
    public required double ElapsedMilliseconds { get; init; }
}
