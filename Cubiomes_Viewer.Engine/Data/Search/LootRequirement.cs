using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Loot;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// An item the containers of a structure have to hold when they are first opened: at least <see cref="MinCount"/> of it in one
/// container or in all of them together, optionally carrying an enchantment.
/// </summary>
public sealed record LootRequirement
{
    public const int MaxCount = 9999;

    /// <summary>The item id, such as <c>minecraft:diamond</c>; books with enchantments are <c>minecraft:enchanted_book</c>.</summary>
    public required string Item { get; init; }

    public int MinCount { get; init; } = 1;

    public LootScope Scope { get; init; } = LootScope.OneChest;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LootEnchantment? Enchantment { get; init; }

    public LootQuery ToQuery() => new(Item, Enchantment?.Id, Enchantment?.MinLevel ?? 1);

    /// <summary>Whether the containers of a structure hold enough of the item, given how many of it they hold.</summary>
    public bool IsMetBy(LootCount count) => (Scope == LootScope.AllChests ? count.Total : count.MostInOne) >= MinCount;
}
