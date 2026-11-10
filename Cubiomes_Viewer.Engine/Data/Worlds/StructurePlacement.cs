namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>
/// Where a structure type generates in one Minecraft version: its dimension and the side length of the
/// square regions that each hold at most one generation attempt.
/// </summary>
public readonly record struct StructurePlacement(Dimension Dimension, int RegionBlocks);
