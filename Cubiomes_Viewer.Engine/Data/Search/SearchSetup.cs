namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>What a search looks for, which seeds it scans and when it stops.</summary>
public sealed record SearchSetup
{
    public const int DefaultMaxResults = 100;
    public const int MaxResultsLimit = 100_000;

    public required SearchDefinition Definition { get; init; }

    public SeedSource Source { get; init; } = new SeedRangeSource();

    /// <summary>The search stops once it has found this many seeds.</summary>
    public int MaxResults { get; init; } = DefaultMaxResults;

    /// <summary>How many workers scan at the same time; zero leaves one of them to the map.</summary>
    public int Workers { get; init; }
}
