namespace Cubiomes_Viewer.Engine.Data.Loot;

/// <summary>The containers of one structure and what they hold.</summary>
public sealed class StructureLoot
{
    /// <summary>The loot of a structure whose containers the generator cannot tell.</summary>
    public static StructureLoot Unknown { get; } = new() { IsKnown = false, Containers = [] };

    public required bool IsKnown { get; init; }

    /// <summary>The containers in the order the structure's pieces generate; empty when <see cref="IsKnown"/> is false.</summary>
    public required LootContainer[] Containers { get; init; }

    public static StructureLoot Of(LootContainer[] containers) => new() { IsKnown = true, Containers = containers };
}
