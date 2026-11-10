namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>One piece of an end city or a fortress. <see cref="Type"/> is one of the piece kinds of that structure.</summary>
public readonly record struct StructurePiece(int Type, int Rotation, int Depth, BlockBox Box);
