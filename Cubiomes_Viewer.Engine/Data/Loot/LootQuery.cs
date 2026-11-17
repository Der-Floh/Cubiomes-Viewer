namespace Cubiomes_Viewer.Engine.Data.Loot;

/// <summary>
/// What a structure's containers are searched for: the stacks of one item, or only those carrying an enchantment of at least a
/// level. <c>minecraft:enchanted_book</c> counts books with enchantments, <c>minecraft:book</c> only those without.
/// </summary>
public sealed record LootQuery(string Item, string? Enchantment = null, int MinLevel = 1);
