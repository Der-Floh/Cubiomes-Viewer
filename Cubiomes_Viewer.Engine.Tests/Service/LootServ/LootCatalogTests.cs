using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LootServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.LootServ;

public sealed class LootCatalogTests
{
    private const long Seed = 262;
    private const int StructuresPerCheck = 24;

    private readonly LootCatalog _catalog = new();

    [Theory]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_12_2, false)]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_13_2, true)]
    [InlineData(StructureType.Outpost, McVersion.V1_13_2, false)]
    [InlineData(StructureType.Outpost, McVersion.V1_14_4, true)]
    [InlineData(StructureType.RuinedPortal, McVersion.V1_15_2, false)]
    [InlineData(StructureType.RuinedPortal, McVersion.V1_16_1, true)]
    [InlineData(StructureType.Bastion, McVersion.V1_16_1, false)]
    [InlineData(StructureType.Bastion, McVersion.V1_16_5, true)]
    [InlineData(StructureType.RuinedPortalNether, McVersion.V1_17_1, false)]
    [InlineData(StructureType.RuinedPortalNether, McVersion.V1_18_2, true)]
    [InlineData(StructureType.AbandonedCamp, McVersion.V26_2, false)]
    [InlineData(StructureType.AbandonedCamp, McVersion.V26_3, true)]
    [InlineData(StructureType.Stronghold, McVersion.V1_12_2, false)]
    [InlineData(StructureType.Stronghold, McVersion.V1_13_2, true)]
    [InlineData(StructureType.EndCity, McVersion.V26_3, true)]
    [InlineData(StructureType.Village, McVersion.V1_21_3, false)]
    [InlineData(StructureType.SwampHut, McVersion.V1_21_3, false)]
    [InlineData(StructureType.TrialChambers, McVersion.V1_21_3, false)]
    public void HasLoot_TypeAndVersion_IsWhetherTheGeneratorTellsTheirChests(StructureType type, McVersion version, bool expected)
        => Assert.Equal(expected, _catalog.HasLoot(type, version));

    [Fact]
    public void GetItems_DesertPyramid_ListsItsItemsByName()
    {
        var items = _catalog.GetItems(StructureType.DesertPyramid, McVersion.V1_21_3);

        Assert.Equal(items.OrderBy(item => item.Name, StringComparer.Ordinal), items);
        Assert.Contains(new LootItem("minecraft:diamond", "Diamond", CanBeEnchanted: false), items);
        Assert.Contains(new LootItem("minecraft:enchanted_book", "Enchanted Book", CanBeEnchanted: true), items);
        Assert.DoesNotContain(items, item => item.Id == "minecraft:book");
    }

    [Fact]
    public void GetItems_Stronghold_ListsPlainAndEnchantedBooksApart()
    {
        var items = _catalog.GetItems(StructureType.Stronghold, McVersion.V1_21_3);

        Assert.Contains(new LootItem("minecraft:book", "Book", CanBeEnchanted: false), items);
        Assert.Contains(new LootItem("minecraft:enchanted_book", "Enchanted Book", CanBeEnchanted: true), items);
    }

    [Fact]
    public void GetItems_ChainRenamedIn1219_HasTheNameOfTheVersion()
    {
        Assert.Contains(_catalog.GetItems(StructureType.Bastion, McVersion.V1_21_3), item => item.Id == "minecraft:chain");
        Assert.Contains(_catalog.GetItems(StructureType.Bastion, McVersion.V1_21_9), item => item.Id == "minecraft:iron_chain");
    }

    [Theory]
    [InlineData(StructureType.Village, McVersion.V1_21_3)]
    [InlineData(StructureType.Outpost, McVersion.V1_13_2)]
    public void GetItems_TypeOrVersionWithoutLoot_IsEmpty(StructureType type, McVersion version)
        => Assert.Empty(_catalog.GetItems(type, version));

    [Fact]
    public void GetEnchantments_EndCityIronSword_ListsWhatItsTableGivesUpToTheHighestLevel()
    {
        var options = _catalog.GetEnchantments(StructureType.EndCity, McVersion.V1_21_3, "minecraft:iron_sword");

        Assert.Equal(options.OrderBy(option => option.Name, StringComparer.Ordinal), options);
        Assert.Contains(new LootEnchantmentOption("minecraft:sharpness", "Sharpness", 5), options);
        Assert.Contains(new LootEnchantmentOption("minecraft:mending", "Mending", 1), options);
        Assert.DoesNotContain(options, option => option.Id == "minecraft:protection");
    }

    [Theory]
    [InlineData(StructureType.DesertPyramid, "minecraft:diamond")]
    [InlineData(StructureType.DesertPyramid, "minecraft:nether_star")]
    [InlineData(StructureType.Stronghold, "minecraft:book")]
    [InlineData(StructureType.Village, "minecraft:enchanted_book")]
    public void GetEnchantments_ItemTheContainersNeverEnchant_IsEmpty(StructureType type, string item)
        => Assert.Empty(_catalog.GetEnchantments(type, McVersion.V1_21_3, item));

    [Theory]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_21_3)]
    [InlineData(StructureType.Stronghold, McVersion.V1_21_3)]
    [InlineData(StructureType.EndCity, McVersion.V1_21_3)]
    [InlineData(StructureType.Shipwreck, McVersion.V1_21_3)]
    [InlineData(StructureType.RuinedPortal, McVersion.V1_21_3)]
    [InlineData(StructureType.Bastion, McVersion.V1_20_6)]
    [InlineData(StructureType.Outpost, McVersion.V1_21_9)]
    [InlineData(StructureType.AbandonedCamp, McVersion.V26_3)]
    public void Pickers_EverythingTheContainersRoll_IsOffered(StructureType type, McVersion version)
    {
        using var generator = CreateGenerator(type, version);
        var items = _catalog.GetItems(type, version).ToDictionary(item => item.Id, StringComparer.Ordinal);
        var stacks = StructureSamples.FindSome(generator, type, StructuresPerCheck)
            .Select(position => generator.FindLoot(type, position))
            .OfType<StructureLoot>()
            .SelectMany(loot => loot.Containers)
            .SelectMany(container => container.Stacks)
            .ToArray();

        Assert.NotEmpty(stacks);
        Assert.All(stacks, stack =>
        {
            Assert.True(items.TryGetValue(stack.Item, out var item), $"{stack.Item} is not offered.");
            Assert.True(stack.Enchantments.Length == 0 || item.CanBeEnchanted, $"{stack.Item} is not offered as enchanted.");
            var options = _catalog.GetEnchantments(type, version, stack.Item);
            Assert.All(stack.Enchantments, enchantment =>
            {
                var option = Assert.Single(options, option => option.Id == enchantment.Enchantment);
                Assert.InRange(enchantment.Level, 1, option.MaxLevel);
            });
        });
    }

    [Theory]
    [InlineData("minecraft:heart_of_the_sea", "Heart of the Sea")]
    [InlineData("minecraft:flint_and_steel", "Flint and Steel")]
    [InlineData("minecraft:enchanted_golden_apple", "Enchanted Golden Apple")]
    [InlineData("minecraft:experience_bottle", "Bottle o' Enchanting")]
    [InlineData("minecraft:tnt", "TNT")]
    public void GetItemName_Item_IsItsEnglishName(string item, string expected)
        => Assert.Equal(expected, _catalog.GetItemName(item));

    [Theory]
    [InlineData("minecraft:sharpness", 3, "Sharpness III")]
    [InlineData("minecraft:luck_of_the_sea", 2, "Luck of the Sea II")]
    [InlineData("minecraft:efficiency", 12, "Efficiency 12")]
    [InlineData("minecraft:mending", 1, "Mending")]
    [InlineData("minecraft:binding_curse", 1, "Curse of Binding")]
    public void GetEnchantmentName_EnchantmentAtALevel_HasANumeralUnlessTheEnchantmentHasOneLevel(string enchantment, int level, string expected)
        => Assert.Equal(expected, _catalog.GetEnchantmentName(enchantment, level));

    [Fact]
    public void GetEffectName_Effect_IsItsEnglishName()
        => Assert.Equal("Fire Resistance", _catalog.GetEffectName("minecraft:fire_resistance"));

    private static CubiomesWorldGenerator CreateGenerator(StructureType type, McVersion version)
    {
        var dimension = new CubiomesStructureFinder().TryGetPlacement(type, version, out var placement) ? placement.Dimension : Dimension.Overworld;
        var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, Seed);
        return generator;
    }
}
