using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Engine.Extensions;

public static class LayerModeExtensions
{
    extension(LayerMode mode)
    {
        /// <summary>Whether the samples of the layer are biome ids rather than numbers.</summary>
        public bool HasBiomeSamples => mode is LayerMode.Biomes or LayerMode.Rivers or LayerMode.OceanTemperature;

        /// <summary>Whether the layer changes with the height it is sampled at.</summary>
        public bool DependsOnHeight => mode is LayerMode.Biomes or LayerMode.Depth;

        /// <summary>Returns the climate parameter the layer shows, or <see langword="null"/> when it shows something else.</summary>
        public ClimateParameter? ToClimateParameter() => mode switch
        {
            LayerMode.Temperature => ClimateParameter.Temperature,
            LayerMode.Humidity => ClimateParameter.Humidity,
            LayerMode.Continentalness => ClimateParameter.Continentalness,
            LayerMode.Erosion => ClimateParameter.Erosion,
            LayerMode.Depth => ClimateParameter.Depth,
            LayerMode.Weirdness => ClimateParameter.Weirdness,
            _ => null,
        };

        /// <summary>Returns the inner layer of the layered generator the layer shows, or <see langword="null"/>.</summary>
        public GeneratorLayer? ToGeneratorLayer() => mode switch
        {
            LayerMode.Rivers => GeneratorLayer.River,
            LayerMode.OceanTemperature => GeneratorLayer.OceanTemperature,
            _ => null,
        };
    }
}
