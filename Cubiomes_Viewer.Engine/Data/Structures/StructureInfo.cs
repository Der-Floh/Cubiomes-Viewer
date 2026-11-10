using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>
/// How a structure type is presented on the map. Beyond <see cref="MaxBlocksPerPixel"/> its
/// positions lie too close together on screen to be told apart.
/// </summary>
public sealed record StructureInfo(StructureType Type, string Name, double MaxBlocksPerPixel);
