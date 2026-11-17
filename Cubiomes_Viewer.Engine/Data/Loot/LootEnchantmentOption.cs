namespace Cubiomes_Viewer.Engine.Data.Loot;

/// <summary>An enchantment the containers of a structure type put on an item, up to the highest level they give it there.</summary>
public sealed record LootEnchantmentOption(string Id, string Name, int MaxLevel);
