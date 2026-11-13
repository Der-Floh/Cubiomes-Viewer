namespace Cubiomes_Viewer.Data;

public static class Constants
{
    public const string AppName = "Cubiomes Viewer";
    public const string RepositoryUrl = "https://github.com/Der-Floh/Cubiomes-Viewer";
    public const double ScreenDpi = 96;

    /// <summary>Views narrower than this use the compact (mobile) shell.</summary>
    public const double CompactLayoutMaxWidth = 720;

    public const int MinSampleHeight = -64;
    public const int MaxSampleHeight = 320;

    /// <summary>How far one notch of the wheel with Shift moves the Y level: one biome cell.</summary>
    public const int SampleHeightWheelStep = 4;
}
