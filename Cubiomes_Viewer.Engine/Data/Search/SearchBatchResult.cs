namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>What a worker found in one run of consecutive seeds.</summary>
public sealed class SearchBatchResult
{
    public required long FirstSeed { get; init; }

    /// <summary>The number of seeds the worker looked at.</summary>
    public required int Count { get; init; }

    /// <summary>The seeds that meet every condition, in the order they were looked at.</summary>
    public required long[] Matches { get; init; }

    /// <summary>How long the worker took, without the time the batch waited for it.</summary>
    public required double ElapsedMilliseconds { get; init; }
}
