namespace Cubiomes_Viewer.Engine.Data.Loot;

/// <summary>An enchantment on a stack, such as <c>minecraft:sharpness</c> at level 3.</summary>
public sealed record LootEnchantmentLevel(string Enchantment, int Level);
