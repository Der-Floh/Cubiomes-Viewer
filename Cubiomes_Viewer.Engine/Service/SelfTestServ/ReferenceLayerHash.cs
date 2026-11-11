using System.Buffers.Binary;

using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.SelfTestServ;

/// <summary>
/// Regression values for the map layers other than biomes, for one seed and a square around the origin, recorded
/// from the Windows x64 reference build. A build for another platform or compiler must reproduce them exactly.
/// </summary>
internal static class ReferenceLayerHash
{
    public const long Seed = 262;

    private const int Side = 64;
    private const int SparseSide = 24;
    private const int Height = 16;
    private const int ThreeOctaves = 3;

    public static IReadOnlyList<LayerCase> Cases { get; } =
    [
        Climate("1.21 temperature", McVersion.V1_21_3, ClimateParameter.Temperature, 4, 0x634c8acd),
        Climate("1.21 humidity", McVersion.V1_21_3, ClimateParameter.Humidity, 4, 0x269d9159),
        Climate("1.21 continentalness", McVersion.V1_21_3, ClimateParameter.Continentalness, 4, 0x1fbc40ff),
        Climate("1.21 erosion", McVersion.V1_21_3, ClimateParameter.Erosion, 4, 0x4f4a8ec1),
        Climate("1.21 depth", McVersion.V1_21_3, ClimateParameter.Depth, 4, 0x2a9121f6),
        Climate("1.21 weirdness", McVersion.V1_21_3, ClimateParameter.Weirdness, 4, 0x2d401e65),
        Climate("1.21 continentalness at 1:64", McVersion.V1_21_3, ClimateParameter.Continentalness, 64, 0x25b2af34),
        Climate("1.21 continentalness, 3 octaves", McVersion.V1_21_3, ClimateParameter.Continentalness, 4, 0xb9f77736, ThreeOctaves),
        Climate("1.21 depth, 3 octaves", McVersion.V1_21_3, ClimateParameter.Depth, 16, 0x7fe9a45e, ThreeOctaves),
        Climate("1.18 erosion", McVersion.V1_18_2, ClimateParameter.Erosion, 16, 0x63ede311),
        Climate("Beta 1.7 temperature", McVersion.Beta1_7, ClimateParameter.Temperature, 1, 0x99bfd306),
        Climate("Beta 1.7 humidity", McVersion.Beta1_7, ClimateParameter.Humidity, 16, 0xe54ecb54),
        Layer("1.16 rivers", McVersion.V1_16_5, GeneratorLayer.River, 4, 0x7b6256cd),
        Layer("1.16 rivers at 1:16", McVersion.V1_16_5, GeneratorLayer.River, 16, 0x4c8378bd),
        Layer("1.6 rivers", McVersion.V1_6_4, GeneratorLayer.River, 4, 0xaaa3f785),
        Layer("Beta 1.8 rivers", McVersion.Beta1_8, GeneratorLayer.River, 4, 0xe0616dc5),
        Layer("1.16 ocean temperatures", McVersion.V1_16_5, GeneratorLayer.OceanTemperature, 4, 0x30eac56b),
        Layer("1.13 ocean temperatures at 1:256", McVersion.V1_13_2, GeneratorLayer.OceanTemperature, 256, 0x4e7ebe3a),
        Heights("1.21 heights", McVersion.V1_21_3, Dimension.Overworld, 4, Side, 0xf78dc343),
        Heights("1.21 heights at 1:64", McVersion.V1_21_3, Dimension.Overworld, 64, Side, 0x0283942e),
        Heights("1.16 heights", McVersion.V1_16_5, Dimension.Overworld, 4, Side, 0x093b7f6d),
        Heights("1.16 heights at 1:16", McVersion.V1_16_5, Dimension.Overworld, 16, SparseSide, 0xd29a7b2f),
        Heights("1.7 heights", McVersion.V1_7_10, Dimension.Overworld, 4, Side, 0xfb930d83),
        Heights("1.21 End heights", McVersion.V1_21_3, Dimension.End, 4, Side, 0xe55c9447),
        Heights("1.21 End heights at 1:16", McVersion.V1_21_3, Dimension.End, 16, SparseSide, 0x2a3d2f84),
        Heights("1.9 End heights", McVersion.V1_9_4, Dimension.End, 4, Side, 0xe55c9447),
    ];

    /// <summary>FNV-1a over the low and the high byte of each value.</summary>
    public static uint Hash(ReadOnlySpan<short> values)
    {
        var bytes = new byte[values.Length * sizeof(short)];
        for (var index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * sizeof(short)), values[index]);
        return ReferenceStructureHash.Hash(bytes);
    }

    private static LayerCase Climate(string name, McVersion version, ClimateParameter parameter, int scale, uint hash, int octaves = 0)
        => new(name, version, Dimension.Overworld, hash, generator =>
        {
            var area = GetArea(scale, Side);
            var values = new short[area.SampleCount];
            generator.GenerateClimate(parameter, octaves, area, values);
            return Hash(values);
        });

    private static LayerCase Layer(string name, McVersion version, GeneratorLayer layer, int scale, uint hash)
        => new(name, version, Dimension.Overworld, hash, generator =>
        {
            var area = GetArea(scale, Side);
            var biomes = new byte[area.SampleCount];
            generator.GenerateLayer(layer, area, biomes);
            return ReferenceStructureHash.Hash(biomes);
        });

    private static LayerCase Heights(string name, McVersion version, Dimension dimension, int scale, int side, uint hash)
        => new(name, version, dimension, hash, generator =>
        {
            var area = GetArea(scale, side);
            var heights = new short[area.SampleCount];
            generator.GenerateHeights(area, heights);
            return Hash(heights);
        });

    private static BiomeArea GetArea(int scale, int side) => new(scale, -side / 2, -side / 2, side, side, Height);

    public sealed record LayerCase(string Name, McVersion Version, Dimension Dimension, uint Hash, Func<IWorldGenerator, uint> Compute);
}
