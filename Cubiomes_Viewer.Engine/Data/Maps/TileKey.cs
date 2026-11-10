namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// Addresses one tile of a level: <see cref="X"/> and <see cref="Z"/> count tiles, and each sample of the tile covers <see cref="Scale"/> blocks.
/// </summary>
public readonly record struct TileKey(int Scale, int X, int Z);
