using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SeedServ;

public sealed class SeedMathTests
{
    private const long Seed = 262;
    private const long SeedWithOceans = 0;
    private const int AreaScale = 4;
    private const int AreaSide = 96;
    private const int WideAreaSide = 512;
    private const byte Ocean = 0;
    private static readonly byte[] _oceans = [Ocean, 10, 24, 44, 45, 46, 47, 48, 49, 50];

    [Theory]
    [InlineData(0L)]
    [InlineData(262L)]
    [InlineData(-4172144997902289642L)]
    [InlineData(long.MaxValue)]
    public void GetShadowSeed_AppliedTwice_ReturnsTheSeed(long seed)
        => Assert.Equal(seed, SeedMath.GetShadowSeed(SeedMath.GetShadowSeed(seed)));

    [Theory]
    [InlineData(McVersion.Beta1_8)]
    [InlineData(McVersion.V1_0_0)]
    [InlineData(McVersion.V1_7_10)]
    [InlineData(McVersion.V1_12_2)]
    [InlineData(McVersion.V1_13_2)]
    [InlineData(McVersion.V1_16_5)]
    [InlineData(McVersion.V1_17_1)]
    public void GetShadowSeed_LayeredOverworld_HasTheSameBiomes(McVersion version)
    {
        var shadow = SeedMath.GetShadowSeed(Seed);

        Assert.True(SeedMath.HasShadowBiomes(version));
        Assert.NotEqual(Seed, shadow);
        Assert.Equal(GenerateBiomes(version, Seed), GenerateBiomes(version, shadow));
    }

    [Theory]
    [InlineData(McVersion.V1_12_2)]
    [InlineData(McVersion.V1_13_2)]
    [InlineData(McVersion.V1_16_5)]
    [InlineData(McVersion.V1_17_1)]
    public void GetShadowSeed_AreaWithOceans_HasTheSameBiomesApartFromTheKindOfOcean(McVersion version)
    {
        var biomes = GenerateBiomes(version, SeedWithOceans, WideAreaSide);
        var shadowBiomes = GenerateBiomes(version, SeedMath.GetShadowSeed(SeedWithOceans), WideAreaSide);

        Assert.Contains(Ocean, biomes.Select(WithoutOceanKind));
        Assert.Equal(biomes.Select(WithoutOceanKind), shadowBiomes.Select(WithoutOceanKind));
    }

    [Theory]
    [InlineData(McVersion.V1_12_2, true)]
    [InlineData(McVersion.V1_13_2, false)]
    [InlineData(McVersion.V1_16_5, false)]
    [InlineData(McVersion.V1_17_1, false)]
    public void GetShadowSeed_AreaWithOceans_KeepsTheKindOfOceanOnlyBefore1_13(McVersion version, bool keepsOceans)
    {
        var biomes = GenerateBiomes(version, SeedWithOceans, WideAreaSide);
        var shadowBiomes = GenerateBiomes(version, SeedMath.GetShadowSeed(SeedWithOceans), WideAreaSide);

        Assert.Equal(keepsOceans, biomes.SequenceEqual(shadowBiomes));
    }

    [Theory]
    [InlineData(McVersion.Beta1_7)]
    [InlineData(McVersion.V1_18_2)]
    [InlineData(McVersion.V1_21_3)]
    public void GetShadowSeed_NoiseBasedOverworld_HasOtherBiomes(McVersion version)
    {
        Assert.False(SeedMath.HasShadowBiomes(version));
        Assert.NotEqual(GenerateBiomes(version, Seed), GenerateBiomes(version, SeedMath.GetShadowSeed(Seed)));
    }

    [Fact]
    public void GetShadowSeed_LayeredOverworld_HasOtherStructures()
    {
        var area = new BlockArea(-4096, -4096, 8192, 8192);

        Assert.NotEqual(FindVillages(Seed, area), FindVillages(SeedMath.GetShadowSeed(Seed), area));
    }

    /// <summary>Folds every ocean biome into one, because a warm temperature also makes a deep ocean shallow.</summary>
    private static byte WithoutOceanKind(byte biome) => _oceans.Contains(biome) ? Ocean : biome;

    private static byte[] GenerateBiomes(McVersion version, long seed, int side = AreaSide)
    {
        using var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, seed);

        var area = new BiomeArea(AreaScale, -side / 2, -side / 2, side, side, 16);
        var biomes = new byte[area.SampleCount];
        generator.GenerateBiomes(area, biomes);
        return biomes;
    }

    private static BlockPos[] FindVillages(long seed, BlockArea area)
    {
        using var generator = new CubiomesWorldGenerator(McVersion.V1_16_5, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, seed);
        return generator.FindStructures(StructureType.Village, area);
    }
}
