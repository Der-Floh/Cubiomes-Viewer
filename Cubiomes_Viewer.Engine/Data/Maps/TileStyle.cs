using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// The choices of the user about how tiles are painted. They do not change what a tile contains.
/// With <see cref="PotentialOf"/> set, biomes in which that structure type cannot generate are dimmed.
/// </summary>
public readonly record struct TileStyle(HeightStyle Height = HeightStyle.Tinted, StructureType? PotentialOf = null);
