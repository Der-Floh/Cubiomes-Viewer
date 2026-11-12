using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

public sealed class BiomeCatalogTests
{
    private const int Plains = 1;
    private const int NetherWastes = 8;
    private const int TheEnd = 9;
    private const int SmallEndIslands = 40;
    private const int DeepWarmOcean = 47;
    private const int SoulSandValley = 170;
    private const int BasaltDeltas = 173;
    private const int CherryGrove = 185;

    private readonly BiomeCatalog _catalog = new();

    [Fact]
    public void GetName_Plains_ReturnsResourceName()
        => Assert.Equal("plains", _catalog.GetName(McVersion.V1_21_3, Plains));

    [Fact]
    public void FromRgb_WrongLength_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => BiomePalette.FromRgb(new byte[Constants.BiomeColorChannels]));

    [Theory]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld, Plains, true)]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld, CherryGrove, true)]
    [InlineData(McVersion.V1_19_2, Dimension.Overworld, CherryGrove, false)]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld, NetherWastes, false)]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld, DeepWarmOcean, false)]
    [InlineData(McVersion.V1_21_3, Dimension.Nether, NetherWastes, true)]
    [InlineData(McVersion.V1_21_3, Dimension.Nether, Plains, false)]
    [InlineData(McVersion.V1_15_2, Dimension.Nether, SoulSandValley, false)]
    [InlineData(McVersion.V1_8_9, Dimension.End, SmallEndIslands, false)]
    [InlineData(McVersion.V1_9_4, Dimension.End, SmallEndIslands, true)]
    [InlineData(McVersion.V1_21_3, Dimension.End, -1, false)]
    [InlineData(McVersion.V1_21_3, Dimension.End, 256, false)]
    public void Generates_Biome_TellsWhetherItGeneratesInThatDimensionAndVersion(McVersion version, Dimension dimension, int biome, bool expected)
        => Assert.Equal(expected, _catalog.Generates(version, dimension, biome));

    [Fact]
    public void GetBiomes_Nether_ListsItsFiveBiomesSince116()
    {
        Assert.Equal([NetherWastes, SoulSandValley, 171, 172, BasaltDeltas], _catalog.GetBiomes(McVersion.V1_21_3, Dimension.Nether));
        Assert.Equal([NetherWastes], _catalog.GetBiomes(McVersion.V1_15_2, Dimension.Nether));
    }

    [Fact]
    public void GetBiomes_End_ListsTheMainIslandAndTheOuterIslands()
        => Assert.Equal([TheEnd, SmallEndIslands, 41, 42, 43], _catalog.GetBiomes(McVersion.V1_21_3, Dimension.End));

    [Fact]
    public void GetBiomes_Overworld_HasOnlyBiomesWithNames()
    {
        var biomes = _catalog.GetBiomes(McVersion.V1_21_3, Dimension.Overworld);

        Assert.InRange(biomes.Count, 50, 70);
        Assert.All(biomes, biome => Assert.NotNull(_catalog.GetDisplayName(McVersion.V1_21_3, biome)));
        Assert.DoesNotContain(NetherWastes, biomes);
    }
}
