namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>
/// The lower 48 bits of a seed plus the salt of a structure type, such that four structures of the type attempt to
/// generate in regions (0, 0) to (1, 1) close enough together for one point to be in range of all of them.
/// </summary>
/// <param name="Value">The base itself.</param>
/// <param name="Quality">The class of its constellation.</param>
/// <param name="Radius">The radius, in blocks, of the smallest sphere around a block that reaches into each of the four huts.</param>
public readonly record struct QuadHutBase(ulong Value, QuadQuality Quality, float Radius);
