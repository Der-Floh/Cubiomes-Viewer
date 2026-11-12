using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Engine.Extensions;

public static class ClimateParameterExtensions
{
    extension(ClimateParameter parameter)
    {
        /// <summary>The map layer that shows the parameter.</summary>
        public LayerMode ToLayerMode() => parameter switch
        {
            ClimateParameter.Temperature => LayerMode.Temperature,
            ClimateParameter.Humidity => LayerMode.Humidity,
            ClimateParameter.Continentalness => LayerMode.Continentalness,
            ClimateParameter.Erosion => LayerMode.Erosion,
            ClimateParameter.Depth => LayerMode.Depth,
            ClimateParameter.Weirdness => LayerMode.Weirdness,
            _ => throw new NotSupportedException($"Unsupported climate parameter: {parameter}"),
        };
    }
}
