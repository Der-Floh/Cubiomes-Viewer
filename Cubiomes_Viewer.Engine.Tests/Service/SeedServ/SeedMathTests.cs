using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SeedServ;

public sealed class SeedMathTests
{
    private const long Seed = 262;
    private const int AreaScale = 4;
    private const int AreaSide = 96;

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

    private static byte[] GenerateBiomes(McVersion version, long seed)
    {
        using var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, seed);

        var area = new BiomeArea(AreaScale, -AreaSide / 2, -AreaSide / 2, AreaSide, AreaSide, 16);
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
