namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>The salt a structure type is placed with instead of the one the game uses.</summary>
public readonly record struct StructureSalt(StructureType Type, int Salt);
