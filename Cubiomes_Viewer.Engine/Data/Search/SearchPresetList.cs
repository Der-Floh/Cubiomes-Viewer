namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>The user's own presets as they are stored.</summary>
public sealed record SearchPresetList
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IReadOnlyList<SearchPreset> Presets { get; init; } = [];
}
