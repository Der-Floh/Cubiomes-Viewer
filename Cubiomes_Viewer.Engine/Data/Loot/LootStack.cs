namespace Cubiomes_Viewer.Engine.Data.Loot;

/// <summary>One stack of items in a container.</summary>
public sealed class LootStack
{
    /// <summary>The item id, such as <c>minecraft:diamond</c>. A book with enchantments is <c>minecraft:enchanted_book</c>, as in the game.</summary>
    public required string Item { get; init; }

    public required int Count { get; init; }

    public LootEnchantmentLevel[] Enchantments { get; init; } = [];

    /// <summary>The effect of a potion or a suspicious stew, such as <c>minecraft:fire_resistance</c>.</summary>
    public string? Effect { get; init; }

    /// <summary>How long <see cref="Effect"/> lasts, in game ticks.</summary>
    public int EffectTicks { get; init; }
}
