namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>How one structure generates, as far as the generator can tell without building the world.</summary>
public sealed record StructureVariant
{
    public const int None = -1;

    public bool IsAbandoned { get; init; }
    public bool IsGiant { get; init; }
    public bool IsUnderground { get; init; }
    public bool HasAirPocket { get; init; }
    public bool HasBasement { get; init; }
    public bool IsCracked { get; init; }
    public bool IsMirrored { get; init; }

    /// <summary>A size whose meaning depends on the structure type, such as the depth of the shaft below an igloo.</summary>
    public int Size { get; init; }

    /// <summary>The index of the starting piece, or <see cref="None"/> when the type has no variants of it.</summary>
    public int Start { get; init; } = None;

    /// <summary>The id of the biome that selects the variant, or <see cref="None"/>.</summary>
    public int Biome { get; init; } = None;

    /// <summary>Quarter turns clockwise.</summary>
    public int Rotation { get; init; }

    /// <summary>The height the structure is placed at, or <see langword="null"/> when it follows the terrain.</summary>
    public int? Height { get; init; }

    /// <summary>The first piece of the structure, or <see langword="null"/> when its size is unknown.</summary>
    public BlockBox? Box { get; init; }
}
