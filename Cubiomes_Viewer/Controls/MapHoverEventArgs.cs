using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls;

public sealed class MapHoverEventArgs(BlockPos? position) : EventArgs
{
    /// <summary>The block under the pointer, or <see langword="null"/> when the pointer left the map.</summary>
    public BlockPos? Position { get; } = position;
}
