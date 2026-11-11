using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// How the whole numbers of a layer are read: <see cref="Unit"/> steps make one unit of the value, which the
/// <see cref="Ramp"/> colours and <see cref="Format"/> writes for the user.
/// </summary>
public sealed record LayerValues(int Unit, ColorRamp Ramp, string Format)
{
    public double ToValue(short stored) => stored / (double)Unit;
}
