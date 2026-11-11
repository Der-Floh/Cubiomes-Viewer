using System.Numerics;
using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Biomes;

/// <summary>A set of biome ids from 0 to 255, one bit each. Two sets with the same biomes are equal.</summary>
[JsonConverter(typeof(BiomeSetJsonConverter))]
public readonly record struct BiomeSet
{
    public const int MaxBiome = 255;

    private const int BitsPerWord = 64;

    public static BiomeSet Empty => default;

    public bool IsEmpty => (_bits0 | _bits1 | _bits2 | _bits3) == 0;

    public int Count => BitOperations.PopCount(_bits0) + BitOperations.PopCount(_bits1) + BitOperations.PopCount(_bits2) + BitOperations.PopCount(_bits3);

    private readonly ulong _bits0;
    private readonly ulong _bits1;
    private readonly ulong _bits2;
    private readonly ulong _bits3;

    private BiomeSet(ulong bits0, ulong bits1, ulong bits2, ulong bits3)
    {
        _bits0 = bits0;
        _bits1 = bits1;
        _bits2 = bits2;
        _bits3 = bits3;
    }

    public static BiomeSet Of(params ReadOnlySpan<int> biomes)
    {
        var set = Empty;
        foreach (var biome in biomes)
            set = set.With(biome);
        return set;
    }

    public bool Contains(int biome) => biome is >= 0 and <= MaxBiome && (GetWord(biome / BitsPerWord) & GetMask(biome)) != 0;

    public BiomeSet With(int biome)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(biome);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(biome, MaxBiome);

        var mask = GetMask(biome);
        return (biome / BitsPerWord) switch
        {
            0 => new BiomeSet(_bits0 | mask, _bits1, _bits2, _bits3),
            1 => new BiomeSet(_bits0, _bits1 | mask, _bits2, _bits3),
            2 => new BiomeSet(_bits0, _bits1, _bits2 | mask, _bits3),
            _ => new BiomeSet(_bits0, _bits1, _bits2, _bits3 | mask),
        };
    }

    public BiomeSet Without(int biome)
    {
        if (!Contains(biome))
            return this;

        var mask = ~GetMask(biome);
        return (biome / BitsPerWord) switch
        {
            0 => new BiomeSet(_bits0 & mask, _bits1, _bits2, _bits3),
            1 => new BiomeSet(_bits0, _bits1 & mask, _bits2, _bits3),
            2 => new BiomeSet(_bits0, _bits1, _bits2 & mask, _bits3),
            _ => new BiomeSet(_bits0, _bits1, _bits2, _bits3 & mask),
        };
    }

    /// <summary>The biome ids of the set in ascending order.</summary>
    public int[] ToArray() => [.. Enumerable.Range(0, MaxBiome + 1).Where(Contains)];

    public override string ToString() => string.Join(", ", ToArray());

    private ulong GetWord(int index) => index switch
    {
        0 => _bits0,
        1 => _bits1,
        2 => _bits2,
        _ => _bits3,
    };

    private static ulong GetMask(int biome) => 1UL << (biome % BitsPerWord);
}
