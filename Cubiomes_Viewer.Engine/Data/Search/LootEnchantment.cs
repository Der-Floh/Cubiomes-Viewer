namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>An enchantment the items of a <see cref="LootRequirement"/> have to carry, at <see cref="MinLevel"/> or above.</summary>
public sealed record LootEnchantment
{
    /// <summary>The enchantment id, such as <c>minecraft:mending</c>.</summary>
    public required string Id { get; init; }

    public int MinLevel { get; init; } = 1;
}
