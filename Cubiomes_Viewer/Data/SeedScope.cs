namespace Cubiomes_Viewer.Data;

/// <summary>Which seeds an analysis or an export covers.</summary>
public enum SeedScope
{
    /// <summary>The seed the map shows.</summary>
    ThisSeed,

    /// <summary>The seeds the search found, in the version and biome size it searched.</summary>
    FoundSeeds,
}
