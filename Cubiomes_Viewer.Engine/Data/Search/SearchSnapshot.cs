namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>A search as it can be stored and continued later: the first <see cref="Scanned"/> seeds of its source were scanned.</summary>
public sealed record SearchSnapshot
{
    public required SearchSetup Setup { get; init; }

    public required long Scanned { get; init; }

    /// <summary>The seed the search continues with, or <see langword="null"/> when it is finished or not known yet.</summary>
    public long? NextSeed { get; init; }

    /// <summary>The seeds found so far, in the order of the source.</summary>
    public required IReadOnlyList<long> Results { get; init; }

    /// <summary>Whether every seed was scanned or enough seeds were found.</summary>
    public bool IsFinished { get; init; }

    public bool Equals(SearchSnapshot? other)
        => other is not null && Setup == other.Setup && Scanned == other.Scanned && NextSeed == other.NextSeed && IsFinished == other.IsFinished && Results.SequenceEqual(other.Results);

    public override int GetHashCode() => HashCode.Combine(Setup, Scanned, NextSeed, IsFinished, Results.Count);
}
