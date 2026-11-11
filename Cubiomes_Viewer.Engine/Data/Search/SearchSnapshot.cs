namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>A search as it can be stored and continued later: every seed before <see cref="NextSeed"/> was scanned.</summary>
public sealed record SearchSnapshot
{
    public required SearchSetup Setup { get; init; }

    public required long NextSeed { get; init; }

    /// <summary>The seeds found so far, in ascending order.</summary>
    public required IReadOnlyList<long> Results { get; init; }

    /// <summary>Whether every seed was scanned or enough seeds were found.</summary>
    public bool IsFinished { get; init; }

    public bool Equals(SearchSnapshot? other)
        => other is not null && Setup == other.Setup && NextSeed == other.NextSeed && IsFinished == other.IsFinished && Results.SequenceEqual(other.Results);

    public override int GetHashCode() => HashCode.Combine(Setup, NextSeed, IsFinished, Results.Count);
}
