using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>
/// Everything that determines the structures of one type inside an area; used as the cache key.
/// The dimension of <see cref="World"/> is the dimension the structure type generates in.
/// </summary>
public readonly record struct StructureAreaRequest(WorldKey World, StructureType Type, BlockArea Area);
