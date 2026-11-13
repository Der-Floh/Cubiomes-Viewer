namespace Cubiomes_Viewer.Controls;

public sealed class MapHeightStepEventArgs(int steps) : EventArgs
{
    /// <summary>How many steps the Y level is asked to move; positive steps raise it.</summary>
    public int Steps { get; } = steps;
}
