namespace Cubiomes_Viewer.Engine.Data.Settings;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public AppearanceSettings Appearance { get; set; } = new();
    public MapSettings Map { get; set; } = new();
    public WorldSettings World { get; set; } = new();
    public SessionSettings Session { get; set; } = new();
    public ExportSettings Export { get; set; } = new();
    public ViewSettings View { get; set; } = new();
    public UpdateSettings Update { get; set; } = new();
}
