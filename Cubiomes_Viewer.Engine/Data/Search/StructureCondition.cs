using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Structures of one type generate in the area. The area is in the coordinates of the dimension of that type.</summary>
public sealed record StructureCondition : SearchCondition
{
    public required StructureType Type { get; init; }

    /// <summary>What the structures have to have besides their type; only the full pass can tell.</summary>
    public StructureTrait Trait { get; init; } = StructureTrait.None;
}
