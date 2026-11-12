namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>An inner layer of the layered Overworld generator. The numbers are those of the native generator.</summary>
public enum GeneratorLayer
{
    River = 0,
    OceanTemperature = 1,

    /// <summary>The temperature category of each cell of the special layer, as <see cref="TemperatureCategory"/> numbers.</summary>
    TemperatureCategory = 2,
}
