using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LootServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Engine.Tests.Service.LootServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

public sealed class CubiomesWorldGeneratorLootTests
{
    private const long Seed = 262;
    private const int StacksPerContainer = 27;
    private const int MaxStackSize = 64;
    private const int StructuresPerCheck = 8;
    private const int ParallelGenerators = 8;
    private static readonly BlockArea _area = new(-2048, -2048, 4096, 4096);

    private static readonly McVersion[] _sweptVersions =
    [
        McVersion.V1_13_2, McVersion.V1_14_4, McVersion.V1_16_1, McVersion.V1_16_5, McVersion.V1_18_2,
        McVersion.V1_20_6, McVersion.V1_21_3, McVersion.V1_21_9, McVersion.V26_3,
    ];

    private static readonly Dictionary<StructureType, string[]> _tables = new()
    {
        [StructureType.DesertPyramid] = ["chests/desert_pyramid"],
        [StructureType.JungleTemple] = ["chests/jungle_temple", "chests/jungle_temple_dispenser"],
        [StructureType.Igloo] = ["chests/igloo_chest"],
        [StructureType.Outpost] = ["chests/pillager_outpost"],
        [StructureType.Shipwreck] = ["chests/shipwreck_supply", "chests/shipwreck_map", "chests/shipwreck_treasure"],
        [StructureType.Treasure] = ["chests/buried_treasure"],
        [StructureType.RuinedPortal] = ["chests/ruined_portal"],
        [StructureType.RuinedPortalNether] = ["chests/ruined_portal"],
        [StructureType.Mineshaft] = ["chests/abandoned_mineshaft"],
        [StructureType.Fortress] = ["chests/nether_bridge"],
        [StructureType.Bastion] = ["chests/bastion_bridge", "chests/bastion_other"],
        [StructureType.EndCity] = ["chests/end_city_treasure"],
        [StructureType.Stronghold] = ["chests/stronghold_corridor", "chests/stronghold_crossing", "chests/stronghold_library"],
        [StructureType.AbandonedCamp] = ["barrels/abandoned_camp_barrel", "chests/abandoned_camp_common_chest", "chests/abandoned_camp_secret_chest"],
    };

    public static TheoryData<StructureType, McVersion> LootTypesAndVersions
    {
        get
        {
            var catalog = new LootCatalog();
            TheoryData<StructureType, McVersion> data = [];
            foreach (var type in _tables.Keys)
            {
                foreach (var version in _sweptVersions.Where(version => catalog.HasLoot(type, version)))
                    data.Add(type, version);
            }

            return data;
        }
    }

    [Fact]
    public void FindLoot_OutpostOfTheForkLootTest_HoldsABookWithFortuneOne()
    {
        using var generator = CreateGenerator(McVersion.V1_21_9, Dimension.Overworld, -7594343816690189614);

        var container = Assert.Single(Known(generator.FindLoot(StructureType.Outpost, new BlockPos(1344, -9872))).Containers);

        Assert.Equal("chests/pillager_outpost", container.Table);
        Assert.Equal(LootContainerKind.Chest, container.Kind);
        Assert.Contains(container.Stacks, stack => stack.Item == "minecraft:enchanted_book" && stack.Enchantments.Contains(new LootEnchantmentLevel("minecraft:fortune", 1)));
    }

    [Fact]
    public void FindLoot_JungleTempleOfTheForkLootTest_HasTwoDispensersAndTwoChests()
    {
        using var generator = CreateGenerator(McVersion.V26_2, Dimension.Overworld, -4911581849163216631);

        var containers = Known(generator.FindLoot(StructureType.JungleTemple, new BlockPos(256, -2016))).Containers;

        Assert.Equal(["chests/jungle_temple_dispenser", "chests/jungle_temple_dispenser", "chests/jungle_temple", "chests/jungle_temple"], containers.Select(container => container.Table));
        Assert.Equal([LootContainerKind.Dispenser, LootContainerKind.Dispenser, LootContainerKind.Chest, LootContainerKind.Chest], containers.Select(container => container.Kind));
    }

    [Fact]
    public void FindLoot_MineshaftOfTheForkLootTest_HasItsMinecartsWithChests()
    {
        using var generator = CreateGenerator(McVersion.V26_3, Dimension.Overworld, 3369970434276511809);
        BlockPos[] expected = [new(120, -20), new(104, -17), new(89, 0), new(128, -46), new(156, -65), new(160, -28), new(155, -20), new(154, -17), new(189, -18)];

        var containers = Known(generator.FindLoot(StructureType.Mineshaft, new BlockPos(112, -48))).Containers;

        Assert.Equal(expected, containers.Select(container => container.Position));
        Assert.All(containers, container => Assert.Equal(LootContainerKind.MinecartWithChest, container.Kind));
    }

    [Fact]
    public void FindLoot_CampOfTheForkLootTest_StartsWithTheBarrelInTheTent()
    {
        using var generator = CreateGenerator(McVersion.V26_3, Dimension.Overworld, -3829811542736183482);

        var containers = Known(generator.FindLoot(StructureType.AbandonedCamp, new BlockPos(224, -384))).Containers;

        Assert.True(containers.Length >= 2);
        Assert.Equal("barrels/abandoned_camp_barrel", containers[0].Table);
        Assert.Equal(LootContainerKind.Barrel, containers[0].Kind);
    }

    [Theory]
    [InlineData(StructureType.Village, McVersion.V1_21_3)]
    [InlineData(StructureType.SwampHut, McVersion.V1_21_3)]
    [InlineData(StructureType.TrialChambers, McVersion.V1_21_3)]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_12_2)]
    public void FindLoot_TypeOrVersionWithoutLoot_IsNull(StructureType type, McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld, Seed);
        var position = new BlockPos(256, -512);

        Assert.Null(generator.FindLoot(type, position));
        Assert.Null(generator.CountLoot(type, position, new LootQuery("minecraft:diamond")));
    }

    [Theory]
    [MemberData(nameof(LootTypesAndVersions))]
    public void FindLoot_StructuresOfEveryLootTypeAndVersion_FillTheirContainersFromTheirTables(StructureType type, McVersion version)
    {
        using var generator = CreateGenerator(version, DimensionOf(type, version), Seed);

        var rolled = StructureSamples.FindSome(generator, type, StructuresPerCheck).Select(position => generator.FindLoot(type, position)).OfType<StructureLoot>().ToArray();

        Assert.NotEmpty(rolled);
        Assert.All(rolled, loot => Assert.True(loot.IsKnown));
        var containers = rolled.SelectMany(loot => loot.Containers).ToArray();
        Assert.NotEmpty(containers);
        Assert.All(containers, container =>
        {
            Assert.Contains(container.Table, _tables[type]);
            Assert.InRange(container.Stacks.Length, 0, StacksPerContainer);
            Assert.All(container.Stacks, stack =>
            {
                Assert.StartsWith("minecraft:", stack.Item, StringComparison.Ordinal);
                Assert.InRange(stack.Count, 1, MaxStackSize);
                Assert.All(stack.Enchantments, enchantment => Assert.StartsWith("minecraft:", enchantment.Enchantment, StringComparison.Ordinal));
            });
        });
    }

    [Theory]
    [InlineData(StructureType.Stronghold, McVersion.V1_21_3)]
    [InlineData(StructureType.EndCity, McVersion.V1_21_3)]
    [InlineData(StructureType.Shipwreck, McVersion.V1_21_3)]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_16_5)]
    [InlineData(StructureType.Bastion, McVersion.V1_20_6)]
    public void CountLoot_EveryItemAndEnchantmentRolled_CountsWhatFindLootLists(StructureType type, McVersion version)
    {
        using var generator = CreateGenerator(version, DimensionOf(type, version), Seed);
        var checkedQueries = 0;

        foreach (var position in StructureSamples.FindSome(generator, type, StructuresPerCheck))
        {
            var loot = Known(generator.FindLoot(type, position));
            foreach (var query in QueriesFor(loot))
            {
                Assert.Equal(Count(loot, query), generator.CountLoot(type, position, query));
                checkedQueries++;
            }
        }

        Assert.True(checkedQueries > 0);
    }

    [Theory]
    [InlineData("minecraft:nether_star", null)]
    [InlineData("minecraft:iron_ingot", "minecraft:no_such_enchantment")]
    [InlineData("minecraft:book", "minecraft:mending")]
    public void CountLoot_QueryNoContainerMatches_CountsNothingButTheContainers(string item, string? enchantment)
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld, Seed);
        var stronghold = generator.FindStrongholds()[0];
        var containers = Known(generator.FindLoot(StructureType.Stronghold, stronghold)).Containers.Length;

        var count = generator.CountLoot(StructureType.Stronghold, stronghold, new LootQuery(item, enchantment));

        Assert.Equal(new LootCount(containers, 0, 0), count);
    }

    [Fact]
    public void FindLoot_SeveralGeneratorsAtOnce_RollWhatOneGeneratorRolls()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.End, Seed);
        var cities = generator.FindStructures(StructureType.EndCity, _area);
        var expected = cities.Select(city => LootText.Describe(generator.FindLoot(StructureType.EndCity, city))).ToArray();
        var rolled = new string[ParallelGenerators][];

        Parallel.For(0, ParallelGenerators, worker =>
        {
            using var own = CreateGenerator(McVersion.V1_21_3, Dimension.End, Seed);
            rolled[worker] = [.. cities.Select(city => LootText.Describe(own.FindLoot(StructureType.EndCity, city)))];
        });

        Assert.NotEmpty(expected);
        Assert.All(rolled, loot => Assert.Equal(expected, loot));
    }

    private static StructureLoot Known(StructureLoot? loot)
    {
        var known = loot ?? throw new InvalidOperationException("The generator could not tell the loot.");
        Assert.True(known.IsKnown);
        return known;
    }

    private static IEnumerable<LootQuery> QueriesFor(StructureLoot loot)
    {
        var stacks = loot.Containers.SelectMany(container => container.Stacks).ToArray();
        foreach (var item in stacks.Select(stack => stack.Item).Append("minecraft:book").Append("minecraft:enchanted_book").Distinct())
            yield return new LootQuery(item);

        foreach (var stack in stacks)
        {
            foreach (var enchantment in stack.Enchantments)
            {
                yield return new LootQuery(stack.Item, enchantment.Enchantment, enchantment.Level);
                yield return new LootQuery(stack.Item, enchantment.Enchantment, enchantment.Level + 1);
            }
        }
    }

    private static LootCount Count(StructureLoot loot, LootQuery query)
    {
        var perContainer = loot.Containers.Select(container => container.Stacks.Where(stack => Matches(stack, query)).Sum(stack => stack.Count)).ToArray();
        return new LootCount(perContainer.Length, perContainer.DefaultIfEmpty().Max(), perContainer.Sum());
    }

    private static bool Matches(LootStack stack, LootQuery query)
        => stack.Item == query.Item
            && (query.Enchantment is null || stack.Enchantments.Any(enchantment => enchantment.Enchantment == query.Enchantment && enchantment.Level >= query.MinLevel));

    private static Dimension DimensionOf(StructureType type, McVersion version)
        => new CubiomesStructureFinder().TryGetPlacement(type, version, out var placement) ? placement.Dimension : Dimension.Overworld;

    private static CubiomesWorldGenerator CreateGenerator(McVersion version, Dimension dimension, long seed)
    {
        var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, seed);
        return generator;
    }
}
