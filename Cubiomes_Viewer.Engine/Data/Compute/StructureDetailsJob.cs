using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>Describes one structure; the dimension of <paramref name="World"/> is the one the structure generates in.</summary>
public sealed record StructureDetailsJob(WorldKey World, StructureType Type, BlockPos Position) : ComputeJob<StructureDetails>;
