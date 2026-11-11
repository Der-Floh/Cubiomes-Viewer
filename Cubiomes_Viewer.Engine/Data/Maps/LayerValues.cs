using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// How the whole numbers of a layer are read: <see cref="Unit"/> steps make one unit of the value, which the
/// <see cref="Ramp"/> colours and <see cref="Format"/> writes for the user.
/// </summary>
public sealed record LayerValues(int Unit, ColorRamp Ramp, string Format)
{
    /// <summary>The colours of the grayscale style, for a layer that has one.</summary>
    public ColorRamp? GrayRamp { get; init; }

    public double ToValue(short stored) => stored / (double)Unit;

    /// <summary>The ramp a style colours the layer with.</summary>
    public ColorRamp GetRamp(TileStyle style) => style.Height == HeightStyle.Grayscale && GrayRamp is { } gray ? gray : Ramp;
}
