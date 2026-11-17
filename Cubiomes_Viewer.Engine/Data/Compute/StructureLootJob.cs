using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>Rolls the loot of one structure's containers; the dimension of <paramref name="World"/> is the one the structure generates in.</summary>
public sealed record StructureLootJob(WorldKey World, StructureType Type, BlockPos Position) : ComputeJob<StructureLoot>;
