using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class LootConditionTests : IDisposable
{
    private const McVersion Version = McVersion.V1_21_3;
    private const int SeedCount = 8;

    private static readonly LootRequirement _fiveDiamondsInOneChest = new() { Item = "minecraft:diamond", MinCount = 5 };
    private static readonly LootRequirement _twelveDiamondsInAllChests = new() { Item = "minecraft:diamond", MinCount = 12, Scope = LootScope.AllChests };

    private readonly ConditionTestBench _bench = new();

    public void Dispose() => _bench.Dispose();

    [Fact]
    public void Structure_LootInOneChest_KeepsTheStructuresWithAChestHoldingEnough()
    {
        var (kept, left) = CompareEndCities(_fiveDiamondsInOneChest);

        Assert.True(kept > 0, "No end city held five diamonds in one chest.");
        Assert.True(left > 0, "Every end city held five diamonds in one chest.");
    }

    [Fact]
    public void Structure_LootInAllChests_CountsTheChestsTogether()
    {
        var (kept, left) = CompareEndCities(_twelveDiamondsInAllChests);

        Assert.True(kept > 0, "No end city held twelve diamonds.");
        Assert.True(left > 0, "Every end city held twelve diamonds.");
    }

    [Fact]
    public void Structure_LootInAllChests_KeepsStructuresNoSingleChestOfWhichHoldsEnough()
    {
        var oneChest = _twelveDiamondsInAllChests with { Scope = LootScope.OneChest };
        var spread = 0;
        for (long seed = 0; seed < SeedCount; seed++)
        {
            var together = Evaluate(StructureType.EndCity, _twelveDiamondsInAllChests, seed).ToHashSet();
            spread += together.Except(Evaluate(StructureType.EndCity, oneChest, seed)).Count();
        }

        Assert.True(spread > 0, "Every end city with twelve diamonds held them in one chest.");
    }

    [Fact]
    public void Stronghold_LootWithAnEnchantment_KeepsTheStrongholdsWhoseChestsHoldTheItemWithIt()
    {
        var requirement = new LootRequirement { Item = "minecraft:enchanted_book", Enchantment = new LootEnchantment { Id = "minecraft:unbreaking", MinLevel = 2 } };
        var area = SearchArea.Circle(9000);
        var kept = 0;
        var left = 0;
        for (long seed = 0; seed < SeedCount; seed++)
        {
            var generator = _bench.GetGenerator(Version, seed);
            var strongholds = generator.FindStrongholds().Where(stronghold => area.Contains(ConditionTestBench.Origin, stronghold)).ToList();
            var expected = strongholds.Where(stronghold => Holds(generator, StructureType.Stronghold, stronghold, requirement)).ToList();

            var (_, matches) = _bench.Evaluate(new StrongholdCondition { Id = 1, Area = area, Loot = requirement }, Version, seed, SearchPass.Full);

            Assert.Equal(expected, matches);
            kept += expected.Count;
            left += strongholds.Count - expected.Count;
        }

        Assert.True(kept > 0, "No stronghold held such a book.");
        Assert.True(left > 0, "Every stronghold held such a book.");
    }

    [Fact]
    public void Structure_QuickPass_KeepsTheSameCandidatesWithLoot()
    {
        var plain = new StructureCondition { Id = 1, Type = StructureType.EndCity, Area = SearchArea.Square(3000) };

        var (plainStatus, plainCandidates) = _bench.Evaluate(plain, Version, 3, SearchPass.Quick);
        var (lootStatus, lootCandidates) = _bench.Evaluate(plain with { Loot = _fiveDiamondsInOneChest }, Version, 3, SearchPass.Quick);

        Assert.Equal(plainStatus, lootStatus);
        Assert.Equal(plainCandidates, lootCandidates);
    }

    [Theory]
    [InlineData(StructureType.Village, McVersion.V1_21_3, "minecraft:diamond", 1, LootScope.OneChest, null, 1, "Village chests cannot be read in this version.")]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_12_2, "minecraft:diamond", 1, LootScope.OneChest, null, 1, "Desert pyramid chests cannot be read in this version.")]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_21_3, "minecraft:nether_star", 1, LootScope.OneChest, null, 1, "Desert pyramid chests never hold Nether Star in this version.")]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_21_3, "", 1, LootScope.OneChest, null, 1, "Choose the item the chests hold.")]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_21_3, "minecraft:diamond", 0, LootScope.OneChest, null, 1, "Ask for 1 to 9999 items.")]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_21_3, "minecraft:diamond", 10000, LootScope.AllChests, null, 1, "Ask for 1 to 9999 items.")]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_21_3, "minecraft:diamond", 1, (LootScope)7, null, 1, "Choose whether the items are in one chest or in all chests together.")]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_21_3, "minecraft:diamond", 1, LootScope.OneChest, "minecraft:mending", 1, "Desert pyramid chests never hold an enchanted Diamond.")]
    [InlineData(StructureType.EndCity, McVersion.V1_21_3, "minecraft:iron_sword", 1, LootScope.OneChest, "minecraft:protection", 1, "End city chests never put Protection on Iron Sword.")]
    [InlineData(StructureType.EndCity, McVersion.V1_21_3, "minecraft:iron_sword", 1, LootScope.OneChest, "minecraft:sharpness", 6, "End city chests put Sharpness on Iron Sword up to level 5.")]
    [InlineData(StructureType.EndCity, McVersion.V1_21_3, "minecraft:iron_sword", 1, LootScope.OneChest, "minecraft:sharpness", 0, "Enchantment levels start at 1.")]
    public void Validate_StructureLootTheChestsCannotHold_IsAProblem(
        StructureType type, McVersion version, string item, int count, LootScope scope, string? enchantment, int level, string expected)
    {
        var requirement = new LootRequirement
        {
            Item = item,
            MinCount = count,
            Scope = scope,
            Enchantment = enchantment is null ? null : new LootEnchantment { Id = enchantment, MinLevel = level },
        };

        var problems = Validate(version, new StructureCondition { Id = 1, Type = type, Loot = requirement });

        Assert.Contains(expected, problems);
    }

    [Theory]
    [InlineData(McVersion.V1_12_2, "Stronghold chests cannot be read in this version.")]
    [InlineData(McVersion.V1_21_3, "Stronghold chests never hold Nether Star in this version.")]
    public void Validate_StrongholdLootTheChestsCannotHold_IsAProblem(McVersion version, string expected)
    {
        var condition = new StrongholdCondition { Id = 1, Loot = new LootRequirement { Item = "minecraft:nether_star" } };

        Assert.Equal(expected, Assert.Single(Validate(version, condition)));
    }

    [Theory]
    [InlineData(StructureType.EndCity, "minecraft:iron_sword", "minecraft:sharpness", 5)]
    [InlineData(StructureType.DesertPyramid, "minecraft:enchanted_book", "minecraft:sharpness", 5)]
    [InlineData(StructureType.Shipwreck, "minecraft:emerald", null, 1)]
    public void Validate_LootTheChestsCanHold_HasNoProblem(StructureType type, string item, string? enchantment, int level)
    {
        var requirement = new LootRequirement { Item = item, Enchantment = enchantment is null ? null : new LootEnchantment { Id = enchantment, MinLevel = level } };

        Assert.Empty(Validate(Version, new StructureCondition { Id = 1, Type = type, Loot = requirement }));
    }

    private (int Kept, int Left) CompareEndCities(LootRequirement requirement)
    {
        var kept = 0;
        var left = 0;
        for (long seed = 0; seed < SeedCount; seed++)
        {
            var generator = _bench.GetGenerator(Version, seed, Dimension.End);
            var cities = FindEndCities(generator);
            var expected = cities.Where(city => Holds(generator, StructureType.EndCity, city, requirement)).ToList();

            var matches = Evaluate(StructureType.EndCity, requirement, seed);

            Assert.Equal(expected, matches);
            kept += expected.Count;
            left += cities.Count - expected.Count;
        }

        return (kept, left);
    }

    private List<BlockPos> Evaluate(StructureType type, LootRequirement requirement, long seed)
        => _bench.Evaluate(new StructureCondition { Id = 1, Type = type, Area = SearchArea.Square(3000), Loot = requirement }, Version, seed, SearchPass.Full).Matches;

    private static List<BlockPos> FindEndCities(IWorldGenerator generator)
    {
        var area = SearchArea.Square(3000);
        return [.. generator.FindStructures(StructureType.EndCity, area.ToBlocks(ConditionTestBench.Origin)).Where(city => area.Contains(ConditionTestBench.Origin, city))];
    }

    private static bool Holds(IWorldGenerator generator, StructureType type, BlockPos position, LootRequirement requirement)
    {
        if (generator.FindLoot(type, position) is not { } loot)
            return false;

        var perContainer = loot.Containers.Select(container => container.Stacks.Where(stack => Matches(stack, requirement)).Sum(stack => stack.Count)).ToArray();
        var count = requirement.Scope == LootScope.AllChests ? perContainer.Sum() : perContainer.DefaultIfEmpty().Max();
        return count >= requirement.MinCount;
    }

    private static bool Matches(LootStack stack, LootRequirement requirement)
        => stack.Item == requirement.Item
            && (requirement.Enchantment is not { } enchantment
                || stack.Enchantments.Any(level => level.Enchantment == enchantment.Id && level.Level >= enchantment.MinLevel));

    private static List<string> Validate(McVersion version, SearchCondition condition)
        => [.. TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(version, condition)).Select(problem => problem.Message)];
}
