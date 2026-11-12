using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>The structures of one type in an area, nearest to the reference point first.</summary>
public sealed class StructureListing
{
    public required StructureType Type { get; init; }

    public required StructureEntry[] Entries { get; init; }
}

/// <summary>One structure: where it is and, when it was asked for, its details.</summary>
public sealed record StructureEntry(BlockPos Position, StructureDetails? Details);
