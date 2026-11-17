using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Loot;

/// <summary>One container of a structure and the stacks the game fills it with when it is first opened.</summary>
public sealed class LootContainer
{
    /// <summary>The loot table that fills the container, such as <c>chests/desert_pyramid</c>.</summary>
    public required string Table { get; init; }

    public required LootContainerKind Kind { get; init; }

    /// <summary>Where the container stands; the generator does not tell its height.</summary>
    public required BlockPos Position { get; init; }

    public required LootStack[] Stacks { get; init; }
}
