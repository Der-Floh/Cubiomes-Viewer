namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>What the map shows of a world. The numbers are stored in the settings.</summary>
public enum LayerMode
{
    Biomes = 0,
    Temperature = 1,
    Humidity = 2,
    Continentalness = 3,
    Erosion = 4,
    Depth = 5,
    Weirdness = 6,
    Rivers = 7,
    OceanTemperature = 8,
    Height = 9,

    /// <summary>The biomes, with those dimmed in which a chosen structure type cannot generate.</summary>
    StructurePotential = 10,
}
