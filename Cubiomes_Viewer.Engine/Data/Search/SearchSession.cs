namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>What the search holds between two runs of the app, and what a session file stores.</summary>
public sealed record SearchSession
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>What the search is set up to look for, which may have changed since the last search ran.</summary>
    public required SearchSetup Setup { get; init; }

    /// <summary>The search that ran last, or <see langword="null"/> when none did.</summary>
    public SearchSnapshot? Search { get; init; }
}
