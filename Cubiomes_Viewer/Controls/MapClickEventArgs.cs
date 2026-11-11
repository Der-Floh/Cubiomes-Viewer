using Avalonia;
using Avalonia.Input;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls;

public sealed class MapClickEventArgs(Point position, BlockPos block, PointerType pointerType) : EventArgs
{
    /// <summary>The clicked position inside the map control.</summary>
    public Point Position { get; } = position;

    /// <summary>The block shown at that position.</summary>
    public BlockPos Block { get; } = block;

    public PointerType PointerType { get; } = pointerType;
}
