namespace Cubiomes_Viewer.ViewModels;

/// <summary>The fields of a condition that can ask the chests of its structures for an item.</summary>
public interface ILootConditionFields
{
    LootFieldsViewModel Loot { get; }
}
