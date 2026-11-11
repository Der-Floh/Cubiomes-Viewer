using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.SeedServ;

/// <summary>Pure arithmetic on world seeds.</summary>
public static class SeedMath
{
    private const long ShadowSeedOffset = -7379792620528906219;

    /// <summary>
    /// Returns the shadow of a seed. In the versions that <see cref="HasShadowBiomes"/> accepts, a world and its
    /// shadow have the same Overworld biome layout while structures and terrain differ. Since 1.13 the kind of
    /// each ocean (its temperature, and with it whether it is deep) differs too.
    /// The shadow of the shadow is the seed itself.
    /// </summary>
    public static long GetShadowSeed(long seed) => unchecked(ShadowSeedOffset - seed);

    /// <summary>Whether the Overworld biomes of a version are generated in layers, which a seed shares with its shadow.</summary>
    public static bool HasShadowBiomes(McVersion version) => version is >= McVersion.Beta1_8 and <= McVersion.V1_17_1;
}
