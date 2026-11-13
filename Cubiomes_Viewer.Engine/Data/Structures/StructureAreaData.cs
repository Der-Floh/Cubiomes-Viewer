using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

public sealed class StructureAreaData
{
    public required StructureAreaRequest Request { get; init; }

    /// <summary>The positions of the structures that generate inside the requested area.</summary>
    public required BlockPos[] Positions { get; init; }

    /// <summary>
    /// For each position, the bits of the feature options its structure has; empty when the structure type has no
    /// features in the version of the world.
    /// </summary>
    public ulong[] Options { get; init; } = [];
}
