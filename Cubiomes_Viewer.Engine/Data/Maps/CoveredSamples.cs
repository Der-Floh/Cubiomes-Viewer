namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// The samples of a tile that cover another, finer or equal tile whole: the column <see cref="X"/> and row
/// <see cref="Z"/> of the first of them, and how many of them span the other tile per side.
/// </summary>
public readonly record struct CoveredSamples(int X, int Z, int Count);
