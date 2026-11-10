using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

public sealed class StructureAreaData
{
    public required StructureAreaRequest Request { get; init; }

    /// <summary>The positions of the structures that generate inside the requested area.</summary>
    public required BlockPos[] Positions { get; init; }
}
