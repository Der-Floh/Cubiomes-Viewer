using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>The quads of a structure type whose middle lies in an area of a world, of the quality <see cref="Worst"/> or better.</summary>
public sealed record QuadListRequest(WorldKey World, StructureType Type, SearchArea Area, BlockPos Reference, QuadQuality Worst = QuadQuality.Barely);
