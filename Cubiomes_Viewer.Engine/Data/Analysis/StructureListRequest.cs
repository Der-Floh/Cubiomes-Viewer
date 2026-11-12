using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>
/// The structures of one type that generate in an area of a world, and with <see cref="Describe"/> what the generator
/// knows about each of them. The dimension of <see cref="World"/> does not matter: the type picks its own.
/// </summary>
public sealed record StructureListRequest(WorldKey World, StructureType Type, SearchArea Area, BlockPos Reference, bool Describe);
