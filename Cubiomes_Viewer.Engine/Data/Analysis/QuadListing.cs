using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>The quads of a structure type in an area, best constellation first and then nearest to the reference point.</summary>
public sealed class QuadListing
{
    public required StructureType Type { get; init; }

    public required QuadEntry[] Entries { get; init; }
}

/// <summary>
/// Four structures in range of one point: how close they are, their middle, where each attempts to generate, and how
/// many of them the biomes let generate.
/// </summary>
public sealed record QuadEntry(QuadQuality Quality, BlockPos Middle, BlockPos[] Structures, int Generating);
