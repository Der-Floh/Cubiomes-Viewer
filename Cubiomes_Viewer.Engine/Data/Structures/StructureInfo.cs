using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>
/// How a structure type is presented on the map. Beyond <see cref="MaxBlocksPerPixel"/> it is no longer marked, as finding its
/// positions over the wider view would take too long; nearer in, positions too close to tell apart are marked as groups.
/// </summary>
public sealed record StructureInfo(StructureType Type, string Name, double MaxBlocksPerPixel);
