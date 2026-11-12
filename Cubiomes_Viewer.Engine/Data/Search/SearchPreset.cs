namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>A named search to start from. Its setup is taken over in the version the map shows when it is opened.</summary>
public sealed record SearchPreset
{
    public required string Name { get; init; }

    /// <summary>What the preset looks for, in a sentence or two; empty for the user's own presets.</summary>
    public string Description { get; init; } = string.Empty;

    public required SearchSetup Setup { get; init; }
}
