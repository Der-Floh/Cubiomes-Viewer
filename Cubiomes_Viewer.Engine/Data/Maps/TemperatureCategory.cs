namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// The climate the layered Overworld of 1.7 to 1.17 gives each region at 1:1024, before it picks any biome. The
/// numbers are those of the native generator.
/// </summary>
public enum TemperatureCategory
{
    Oceanic = 0,
    Warm = 1,
    Lush = 2,
    Cold = 3,
    Freezing = 4,

    /// <summary>Warm with the special variant, which turns into badlands plateaus.</summary>
    SpecialWarm = 5,

    /// <summary>Lush with the special variant, which turns into jungles.</summary>
    SpecialLush = 6,

    /// <summary>Cold with the special variant, which turns into giant tree taigas.</summary>
    SpecialCold = 7,
}
