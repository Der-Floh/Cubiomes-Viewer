namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>
/// The lower 48 bits of a seed. They alone decide where structures attempt to generate and where slime chunks are; the
/// 65,536 sister seeds that share them differ only in their biomes.
/// </summary>
public static class StructureSeed
{
    public const int Bits = 48;
    public const long Max = (1L << Bits) - 1;
    public const int SisterSeeds = 1 << (64 - Bits);

    public static long Of(long seed) => seed & Max;

    /// <summary>The sister seed of a structure seed with the given value of the upper 16 bits.</summary>
    public static long ToWorldSeed(long structureSeed, int upperBits) => (structureSeed & Max) | ((long)upperBits << Bits);
}
