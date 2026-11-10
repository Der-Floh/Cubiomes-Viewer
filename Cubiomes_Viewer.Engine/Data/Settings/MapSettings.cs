namespace Cubiomes_Viewer.Engine.Data.Settings;

public sealed class MapSettings
{
    public const int MinCacheMegabytes = 32;
    public const int MaxCacheMegabytes = 4096;
    public const int DefaultCacheMegabytes = 256;

    public int CacheMegabytes { get; set; } = DefaultCacheMegabytes;
    public bool SmoothMotion { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
}
