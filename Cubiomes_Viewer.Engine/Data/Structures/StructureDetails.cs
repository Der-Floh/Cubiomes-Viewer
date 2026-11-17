using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>What the generator knows about one structure beyond its position.</summary>
public sealed class StructureDetails
{
    public required StructureType Type { get; init; }
    public required BlockPos Position { get; init; }

    /// <summary><see langword="null"/> for structure types without variants.</summary>
    public StructureVariant? Variant { get; init; }

    /// <summary>The pieces of an end city or a fortress; empty for other types.</summary>
    public required StructurePiece[] Pieces { get; init; }

    /// <summary>
    /// The height the structure lies at, or <see langword="null"/> where it cannot be told. Only the details of a single picked
    /// structure carry it, as estimating the surface would slow down searches that describe many.
    /// </summary>
    public StructureHeight? Height { get; init; }

    public StructureDetails WithHeight(StructureHeight? height)
        => new() { Type = Type, Position = Position, Variant = Variant, Pieces = Pieces, Height = height };
}
