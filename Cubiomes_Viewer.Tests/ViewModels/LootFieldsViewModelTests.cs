using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class LootFieldsViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();
    private readonly SearchSetupViewModel _setup;
    private readonly WorldSession _session;

    public LootFieldsViewModelTests()
    {
        _setup = _services.GetRequiredService<SearchSetupViewModel>();
        _session = _services.GetRequiredService<WorldSession>();
        _session.Version = McVersion.V1_21_3;
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void LootFields_ChestLootOff_AreHidden()
    {
        var fields = AddStructure(StructureType.EndCity);

        Assert.False(fields.Loot.IsShown);
    }

    [AvaloniaFact]
    public void LootFields_ChestLootTurnedOnInTheSettings_ShowForTypesWhoseChestsCanBeRead()
    {
        _services.GetRequiredService<SettingsViewModel>().ShowChestLoot = true;
        var city = AddStructure(StructureType.EndCity);
        var village = AddStructure(StructureType.Village);

        Assert.True(_setup.ShowChestLoot);
        Assert.True(city.Loot.IsShown);
        Assert.Equal("Anything", city.Loot.Items[0].Display);
        Assert.Contains(city.Loot.Items, choice => choice.Value == "minecraft:diamond");
        Assert.False(village.Loot.IsShown);
    }

    [AvaloniaFact]
    public void LootFields_ItemCountAndScope_GoIntoTheConditionAndItsSummary()
    {
        _setup.ShowChestLoot = true;
        var fields = AddStructure(StructureType.EndCity);

        SelectItem(fields.Loot, "minecraft:diamond");
        fields.Loot.MinCount = 5;
        fields.Loot.SelectedScope = fields.Loot.Scopes.Single(choice => choice.Value == LootScope.AllChests);

        Assert.Equal(new LootRequirement { Item = "minecraft:diamond", MinCount = 5, Scope = LootScope.AllChests }, Condition<StructureCondition>().Loot);
        Assert.StartsWith("holding 5 × Diamond in all chests together · ", _setup.Conditions[^1].Summary, StringComparison.Ordinal);
        Assert.True(fields.Loot.HasItem);
        Assert.False(fields.Loot.HasEnchantments);
        Assert.True(_setup.IsValid);
    }

    [AvaloniaFact]
    public void LootFields_EnchantableItem_OffersItsEnchantmentsUpToTheirHighestLevel()
    {
        _setup.ShowChestLoot = true;
        var fields = AddStructure(StructureType.EndCity);

        SelectItem(fields.Loot, "minecraft:iron_sword");
        Assert.True(fields.Loot.HasEnchantments);
        Assert.False(fields.Loot.HasLevels);
        fields.Loot.SelectedEnchantment = fields.Loot.Enchantments.Single(choice => choice.Value == "minecraft:sharpness");
        fields.Loot.MinLevel = 3;

        Assert.True(fields.Loot.HasLevels);
        Assert.Equal(5, fields.Loot.LevelMaximum);
        Assert.Equal(new LootEnchantment { Id = "minecraft:sharpness", MinLevel = 3 }, Condition<StructureCondition>().Loot?.Enchantment);
        Assert.StartsWith("holding Iron Sword with Sharpness III or more · ", _setup.Conditions[^1].Summary, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void LootFields_TypeChanged_KeepsAnItemTheNewTypeHoldsAndDropsOneItNeverHolds()
    {
        _setup.ShowChestLoot = true;
        var fields = AddStructure(StructureType.EndCity);
        SelectItem(fields.Loot, "minecraft:diamond");

        fields.SelectedType = fields.Types.Single(choice => choice.Value == StructureType.DesertPyramid);
        Assert.Equal("minecraft:diamond", Condition<StructureCondition>().Loot?.Item);

        fields.SelectedType = fields.Types.Single(choice => choice.Value == StructureType.RuinedPortal);
        Assert.Null(Condition<StructureCondition>().Loot);
        Assert.Equal("Anything", fields.Loot.SelectedItem?.Display);
    }

    [AvaloniaFact]
    public void LootFields_VersionWithoutTheLoot_KeepsTheItemAndShowsTheProblem()
    {
        _setup.ShowChestLoot = true;
        var fields = AddStructure(StructureType.Bastion);
        SelectItem(fields.Loot, "minecraft:ancient_debris");

        _session.Version = McVersion.V1_16_1;

        Assert.Equal("minecraft:ancient_debris", Condition<StructureCondition>().Loot?.Item);
        Assert.Equal("minecraft:ancient_debris", fields.Loot.SelectedItem?.Value);
        Assert.Contains("Bastion remnant chests cannot be read in this version.", _setup.Conditions[^1].Problems);
    }

    [AvaloniaFact]
    public void LootFields_ChestLootTurnedOffWhileAnItemIsAsked_StayUntilTheItemIsTakenAway()
    {
        _setup.ShowChestLoot = true;
        var fields = AddStructure(StructureType.EndCity);
        SelectItem(fields.Loot, "minecraft:diamond");

        _setup.ShowChestLoot = false;
        Assert.True(fields.Loot.IsShown);

        fields.Loot.SelectedItem = fields.Loot.Items[0];
        Assert.False(fields.Loot.IsShown);
        Assert.Null(Condition<StructureCondition>().Loot);
    }

    [AvaloniaFact]
    public void StrongholdFields_Loot_GoesIntoTheConditionAndItsSummary()
    {
        _setup.ShowChestLoot = true;
        _setup.ConditionKinds.Single(kind => kind.Descriptor.ConditionType == typeof(StrongholdCondition)).AddCommand.Execute(null);
        var fields = Assert.IsType<StrongholdFieldsViewModel>(_setup.Conditions[^1].Fields);

        SelectItem(fields.Loot, "minecraft:enchanted_book");
        fields.Loot.SelectedEnchantment = fields.Loot.Enchantments[1];

        var requirement = Condition<StrongholdCondition>().Loot;
        Assert.Equal("minecraft:enchanted_book", requirement?.Item);
        Assert.Equal(fields.Loot.Enchantments[1].Value, requirement?.Enchantment?.Id);
        Assert.StartsWith("holding Enchanted Book with ", _setup.Conditions[^1].Summary, StringComparison.Ordinal);
        Assert.True(_setup.IsValid);
    }

    [AvaloniaFact]
    public void Show_SavedConditionWithLootWhileChestLootIsOff_ShowsItsLootFields()
    {
        var condition = new StructureCondition { Id = 1, Type = StructureType.EndCity, Loot = new LootRequirement { Item = "minecraft:diamond", MinCount = 3 } };

        _setup.Show(new SearchSetup { Definition = new SearchDefinition { Version = McVersion.V1_21_3, Conditions = [condition] } });

        var fields = Assert.IsType<StructureFieldsViewModel>(Assert.Single(_setup.Conditions).Fields);
        Assert.True(fields.Loot.IsShown);
        Assert.Equal("minecraft:diamond", fields.Loot.SelectedItem?.Value);
        Assert.Equal(3, fields.Loot.MinCount);
        Assert.Equal(condition, Assert.Single(_setup.Setup.Definition.Conditions));
    }

    private StructureFieldsViewModel AddStructure(StructureType type)
    {
        _setup.ConditionKinds.Single(kind => kind.Descriptor.ConditionType == typeof(StructureCondition)).AddCommand.Execute(null);
        var fields = Assert.IsType<StructureFieldsViewModel>(_setup.Conditions[^1].Fields);
        fields.SelectedType = fields.Types.Single(choice => choice.Value == type);
        return fields;
    }

    private static void SelectItem(LootFieldsViewModel loot, string item) => loot.SelectedItem = loot.Items.Single(choice => choice.Value == item);

    private TCondition Condition<TCondition>() where TCondition : SearchCondition
        => Assert.IsType<TCondition>(_setup.Setup.Definition.Conditions[^1]);
}
