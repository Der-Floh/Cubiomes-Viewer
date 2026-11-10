using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.SelfTestServ;

/// <summary>
/// The biome regression hash of the cubiomes test suite: one biome sample for each of
/// <c>4^bits</c> small seeds, mixed into a single 32-bit value.
/// </summary>
internal static class ReferenceBiomeHash
{
    public const int QuickBits = 6;

    private const int SampleScale = 4;
    private const int HeightRange = 384;
    private const int HeightOffset = 64;
    private const int BiomeCellShift = 2;

    /// <summary>Expected <see cref="QuickBits"/> hashes for the Overworld, taken from cubiomes <c>tests.c</c>.</summary>
    public static IReadOnlyList<(McVersion Version, uint Hash)> QuickOverworldHashes { get; } =
    [
        (McVersion.V1_20_6, 0x0f8888ab),
        (McVersion.V1_19_4, 0x391c36ec),
        (McVersion.V1_19_2, 0xea3e8c1c),
        (McVersion.V1_18_2, 0xade7f891),
        (McVersion.V1_16_5, 0xde9a6574),
        (McVersion.V1_15_2, 0x3a568a6d),
        (McVersion.V1_13_2, 0x96c97323),
        (McVersion.V1_12_2, 0xbc75e996),
        (McVersion.V1_9_4, 0xe27a45a2),
        (McVersion.V1_7_10, 0xbc75e996),
        (McVersion.V1_6_4, 0x15b47206),
        (McVersion.V1_2_5, 0x2d7e0fed),
        (McVersion.V1_1_0, 0x5cbf4709),
        (McVersion.V1_0_0, 0xbd794adb),
    ];

    public static uint Compute(IWorldGenerator generator, Dimension dimension, int bits)
    {
        var radius = 1 << (bits - 1);
        uint hash = 0;

        for (var x = -radius; x < radius; x++)
        {
            for (var z = -radius; z < radius; z++)
            {
                var seed = (z << bits) ^ x;
                generator.Apply(dimension, seed);

                var biome = generator.GetBiomeAt(SampleScale, x, GetSampleHeight(seed), z);
                hash ^= Mix((uint)(seed ^ (biome << (2 * bits))));
            }
        }

        return hash;
    }

    private static int GetSampleHeight(int seed)
        => ((int)((Mix((uint)seed) & int.MaxValue) % HeightRange) - HeightOffset) >> BiomeCellShift;

    private static uint Mix(uint value)
    {
        value ^= value >> 15;
        value *= 0xd168aaad;
        value ^= value >> 15;
        value *= 0xaf723597;
        value ^= value >> 15;
        return value;
    }
}
