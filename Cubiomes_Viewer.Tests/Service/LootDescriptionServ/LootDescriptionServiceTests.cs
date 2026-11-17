using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LootServ;
using Cubiomes_Viewer.Service.LootDescriptionServ;

namespace Cubiomes_Viewer.Tests.Service.LootDescriptionServ;

public sealed class LootDescriptionServiceTests
{
    private readonly LootDescriptionService _service = new(new LootCatalog());

    [Fact]
    public void Describe_ContainersOfDifferentKinds_NumbersOnlyKindsThatComeMoreThanOnce()
    {
        var loot = StructureLoot.Of(
        [
            Container(LootContainerKind.Dispenser, new BlockPos(1, 2), Stack("minecraft:arrow", 3)),
            Container(LootContainerKind.Chest, new BlockPos(-5, 7), Stack("minecraft:bone", 2)),
            Container(LootContainerKind.Chest, new BlockPos(9, -9), Stack("minecraft:bone", 1)),
        ]);

        var chests = _service.Describe(loot);

        Assert.Equal(["Dispenser  ·  X 1, Z 2", "Chest 1  ·  X -5, Z 7", "Chest 2  ·  X 9, Z -9"], chests.Select(chest => chest.Title));
    }

    [Fact]
    public void Describe_StacksOfTheSameItem_AddsThemUpInTheOrderTheyFirstAppear()
    {
        var loot = StructureLoot.Of([Container(LootContainerKind.Chest, default,
            Stack("minecraft:iron_ingot", 3), Stack("minecraft:saddle", 1), Stack("minecraft:iron_ingot", 2), Stack("minecraft:experience_bottle", 4))]);

        Assert.Equal(["5 × Iron Ingot", "Saddle", "4 × Bottle o' Enchanting"], Assert.Single(_service.Describe(loot)).Contents);
    }

    [Fact]
    public void Describe_EnchantedItems_NameTheirEnchantmentsWithTheirLevels()
    {
        var loot = StructureLoot.Of([Container(LootContainerKind.Chest, default,
            Stack("minecraft:enchanted_book", 1, new LootEnchantmentLevel("minecraft:mending", 1)),
            Stack("minecraft:iron_sword", 1, new LootEnchantmentLevel("minecraft:sharpness", 3), new LootEnchantmentLevel("minecraft:unbreaking", 1)),
            Stack("minecraft:enchanted_book", 1, new LootEnchantmentLevel("minecraft:mending", 1)))]);

        Assert.Equal(["2 × Enchanted Book: Mending", "Iron Sword: Sharpness III, Unbreaking I"], Assert.Single(_service.Describe(loot)).Contents);
    }

    [Fact]
    public void Describe_StewsAndPotions_NameTheirEffectAndHowLongItLasts()
    {
        var loot = StructureLoot.Of([Container(LootContainerKind.Chest, default,
            Effect("minecraft:suspicious_stew", "minecraft:night_vision", 100),
            Effect("minecraft:potion", "minecraft:water_breathing", 3600),
            Effect("minecraft:suspicious_stew", "minecraft:saturation", 7))]);

        Assert.Equal(["Suspicious Stew (Night Vision, 0:05)", "Potion (Water Breathing, 3:00)", "Suspicious Stew (Saturation)"], Assert.Single(_service.Describe(loot)).Contents);
    }

    [Fact]
    public void Describe_EmptyContainer_SaysItIsEmpty()
        => Assert.Equal(["Empty"], Assert.Single(_service.Describe(StructureLoot.Of([Container(LootContainerKind.Barrel, default)]))).Contents);

    [Theory]
    [InlineData(StructureType.Mineshaft, McVersion.V1_21_3, true)]
    [InlineData(StructureType.AbandonedCamp, McVersion.V26_3, true)]
    [InlineData(StructureType.RuinedPortal, McVersion.V1_21_3, true)]
    [InlineData(StructureType.Bastion, McVersion.V1_21_3, true)]
    [InlineData(StructureType.EndCity, McVersion.V1_15_2, true)]
    [InlineData(StructureType.EndCity, McVersion.V1_21_3, false)]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_21_3, false)]
    public void DescribeAccuracy_TypeAndVersion_WarnsWhereTheGeneratorEstimates(StructureType type, McVersion version, bool estimates)
        => Assert.Equal(estimates, _service.DescribeAccuracy(type, version) is not null);

    private static LootContainer Container(LootContainerKind kind, BlockPos position, params LootStack[] stacks)
        => new() { Table = "chests/test", Kind = kind, Position = position, Stacks = stacks };

    private static LootStack Stack(string item, int count, params LootEnchantmentLevel[] enchantments)
        => new() { Item = item, Count = count, Enchantments = enchantments };

    private static LootStack Effect(string item, string effect, int ticks)
        => new() { Item = item, Count = 1, Effect = effect, EffectTicks = ticks };
}
