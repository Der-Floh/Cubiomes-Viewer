namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Where the items a <see cref="LootRequirement"/> asks for have to be.</summary>
public enum LootScope
{
    /// <summary>All in one container.</summary>
    OneChest = 0,

    /// <summary>In the structure's containers together.</summary>
    AllChests,
}
