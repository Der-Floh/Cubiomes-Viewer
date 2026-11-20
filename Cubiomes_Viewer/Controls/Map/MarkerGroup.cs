using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// One marker on the map for positions that lie too close together to be told apart: a single position is drawn where it is, a
/// group in the middle of its members with their number.
/// </summary>
/// <param name="X">Where the marker is drawn, in blocks.</param>
/// <param name="Z">Where the marker is drawn, in blocks.</param>
/// <param name="Count">How many positions the marker stands for.</param>
/// <param name="First">The index of the position the group started from; for a single position, that position.</param>
/// <param name="Min">The corner of the members' bounds with the lowest coordinates.</param>
/// <param name="Max">The corner of the members' bounds with the highest coordinates.</param>
public readonly record struct MarkerGroup(double X, double Z, int Count, int First, BlockPos Min, BlockPos Max)
{
    public bool IsSingle => Count == 1;
}
