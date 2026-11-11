using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

public sealed class StructureBiomeRuleTests
{
    private const int Ocean = 0;
    private const int Plains = 1;
    private const int Desert = 2;
    private const int Forest = 4;
    private const int Taiga = 5;
    private const int SnowyPlains = 12;
    private const int DeepOcean = 24;
    private const int Savanna = 35;
    private const int Meadow = 177;
    private const int BasaltDeltas = 173;
    private const int NetherWastes = 8;
    private const int EndHighlands = 42;
    private const int SmallEndIslands = 40;

    private readonly CubiomesStructureFinder _finder = new();
    private readonly StructureCatalog _catalog = new(new CubiomesStructureFinder());

    [Fact]
    public void TryGetViableBiomes_VillageInTheNewestVersion_ListsTheSixVillageBiomes()
    {
        Assert.True(_finder.TryGetViableBiomes(StructureType.Village, McVersion.V1_21_3, out var biomes));

        Assert.Equal([Plains, Desert, Taiga, SnowyPlains, Savanna, Meadow], biomes.Order());
    }

    [Fact]
    public void TryGetViableBiomes_VillageBeforeTaigaVillages_ListsThreeBiomes()
    {
        Assert.True(_finder.TryGetViableBiomes(StructureType.Village, McVersion.V1_9_4, out var biomes));

        Assert.Equal([Plains, Desert, Savanna], biomes.Order());
    }

    [Fact]
    public void TryGetViableBiomes_Monument_NeedsADeepOcean()
    {
        Assert.True(_finder.TryGetViableBiomes(StructureType.Monument, McVersion.V1_21_3, out var biomes));

        Assert.Contains(DeepOcean, biomes);
        Assert.DoesNotContain(Ocean, biomes);
        Assert.DoesNotContain(Forest, biomes);
    }

    [Fact]
    public void TryGetViableBiomes_NetherStructures_DifferInTheBasaltDeltas()
    {
        Assert.True(_finder.TryGetViableBiomes(StructureType.Fortress, McVersion.V1_21_3, out var fortress));
        Assert.True(_finder.TryGetViableBiomes(StructureType.Bastion, McVersion.V1_21_3, out var bastion));

        Assert.Contains(BasaltDeltas, fortress);
        Assert.DoesNotContain(BasaltDeltas, bastion);
        Assert.Contains(NetherWastes, bastion);
    }

    [Fact]
    public void TryGetViableBiomes_EndCity_GeneratesOnTheLargeIslandsOnly()
    {
        Assert.True(_finder.TryGetViableBiomes(StructureType.EndCity, McVersion.V1_21_3, out var biomes));

        Assert.Contains(EndHighlands, biomes);
        Assert.DoesNotContain(SmallEndIslands, biomes);
    }

    [Theory]
    [InlineData(StructureType.Geode)]
    [InlineData(StructureType.Feature)]
    [InlineData(StructureType.EndIsland)]
    public void TryGetViableBiomes_TypeWithoutABiomeRule_ReturnsFalse(StructureType type)
    {
        Assert.False(_finder.TryGetViableBiomes(type, McVersion.V1_21_3, out var biomes));
        Assert.Null(biomes);
    }

    [Fact]
    public void GetWithBiomeRule_NewestOverworld_ListsTheAvailableStructuresExceptGeodes()
    {
        var available = _catalog.GetAvailable(McVersion.V1_21_3, Dimension.Overworld).Select(structure => structure.Type);

        var withRule = _catalog.GetWithBiomeRule(McVersion.V1_21_3, Dimension.Overworld).Select(structure => structure.Type);

        Assert.Equal(available.Where(type => type != StructureType.Geode), withRule);
    }

    [Fact]
    public void GetWithBiomeRule_EveryListedStructure_HasAtLeastOneBiome()
    {
        foreach (var dimension in Enum.GetValues<Dimension>())
        {
            foreach (var structure in _catalog.GetWithBiomeRule(McVersion.V1_21_3, dimension))
            {
                Assert.True(_finder.TryGetViableBiomes(structure.Type, McVersion.V1_21_3, out var biomes));
                Assert.NotEmpty(biomes);
            }
        }
    }
}
