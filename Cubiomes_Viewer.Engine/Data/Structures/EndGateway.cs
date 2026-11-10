using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>A gateway near the main End island and the place in the outer End it leads to.</summary>
public readonly record struct EndGateway(BlockPos Position, BlockPos Destination);
