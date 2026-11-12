namespace Cubiomes_Viewer.Engine.Data.Colors;

/// <summary>Colour maps as they are stored and shared: the app keeps all of its maps in one, an exported file holds one.</summary>
public sealed class BiomeColorMapFile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<BiomeColorMap> Maps { get; set; } = [];
}
