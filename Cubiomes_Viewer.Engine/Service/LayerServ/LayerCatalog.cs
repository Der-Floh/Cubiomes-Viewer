using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.LayerServ;

public sealed class LayerCatalog : ILayerCatalog
{
    private const int CellScale = 4;
    private const int Supported = 1;
    private const string ClimateFormat = "0.00";
    private const string BlockFormat = "0";

    // Each noise has two sets of octaves; the numbers count the octaves with a weight in both sets.
    private const int TemperatureOctaves = 4;
    private const int HumidityOctaves = 4;
    private const int ContinentalnessOctaves = 18;
    private const int ErosionOctaves = 8;
    private const int WeirdnessOctaves = 6;
    private const int DepthOctaves = ContinentalnessOctaves;

    private static readonly LayerMode[] _modes = Enum.GetValues<LayerMode>();

    private readonly Lock _gate = new();
    private readonly Dictionary<(McVersion, Dimension), IReadOnlyList<LayerInfo>> _available = [];

    public IReadOnlyList<LayerInfo> GetAvailable(McVersion version, Dimension dimension)
    {
        lock (_gate)
        {
            if (!_available.TryGetValue((version, dimension), out var layers))
            {
                layers = [.. _modes.Where(mode => IsAvailable(mode, version, dimension)).Select(mode => GetInfo(mode, version, dimension))];
                _available.Add((version, dimension), layers);
            }

            return layers;
        }
    }

    public bool IsAvailable(LayerMode mode, McVersion version, Dimension dimension)
    {
        if (mode == LayerMode.Biomes)
            return true;
        if (mode.ToClimateParameter() is { } parameter)
            return CubiomesNative.ClimateSupported((int)version, (int)dimension, (int)parameter) == Supported;
        if (mode.ToGeneratorLayer() is { } layer)
            return CubiomesNative.LayerSupported((int)version, (int)dimension, (int)layer, CellScale) == Supported;
        return mode == LayerMode.Height && CubiomesNative.HeightSupported((int)version, (int)dimension) == Supported;
    }

    public LayerInfo GetInfo(LayerMode mode, McVersion version, Dimension dimension)
    {
        var hasNoise = version >= McVersion.V1_18_2;
        return mode switch
        {
            LayerMode.Biomes => new LayerInfo { Mode = mode, Name = "Biomes" },
            LayerMode.Temperature => Climate(mode, "Temperature", hasNoise ? LayerRamps.Temperature : LayerRamps.BetaTemperature, hasNoise ? TemperatureOctaves : 0),
            LayerMode.Humidity => Climate(mode, "Humidity", hasNoise ? LayerRamps.Humidity : LayerRamps.BetaHumidity, hasNoise ? HumidityOctaves : 0),
            LayerMode.Continentalness => Climate(mode, "Continentalness", LayerRamps.Continentalness, ContinentalnessOctaves),
            LayerMode.Erosion => Climate(mode, "Erosion", LayerRamps.Erosion, ErosionOctaves),
            LayerMode.Depth => Climate(mode, "Depth", LayerRamps.Depth, DepthOctaves),
            LayerMode.Weirdness => Climate(mode, "Weirdness", LayerRamps.Weirdness, WeirdnessOctaves),
            LayerMode.Rivers => new LayerInfo { Mode = mode, Name = "Rivers" },
            LayerMode.OceanTemperature => new LayerInfo { Mode = mode, Name = "Ocean temperature" },
            LayerMode.Height => new LayerInfo { Mode = mode, Name = "Surface height", Values = GetHeightValues(dimension) },
            _ => throw new NotSupportedException($"Unsupported layer: {mode}"),
        };
    }

    private static LayerValues GetHeightValues(Dimension dimension) => dimension == Dimension.End
        ? new LayerValues(Constants.HeightUnit, LayerRamps.EndHeight, BlockFormat) { GrayRamp = LayerRamps.EndHeightGray }
        : new LayerValues(Constants.HeightUnit, LayerRamps.OverworldHeight, BlockFormat) { GrayRamp = LayerRamps.OverworldHeightGray };

    private static LayerInfo Climate(LayerMode mode, string name, ColorRamp ramp, int maxOctaves)
        => new() { Mode = mode, Name = name, Values = new LayerValues(Constants.ClimateUnit, ramp, ClimateFormat), MaxOctaves = maxOctaves };
}
