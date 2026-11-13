using Avalonia;

namespace Cubiomes_Viewer.Data;

/// <summary>
/// The device pixels a control is drawn on: how many of them make up one unit of the control, and where the control's origin lies
/// on them. Edges snapped to the grid land on pixel boundaries, so every drawing of a shape covers the same pixels.
/// </summary>
public readonly record struct PixelGrid(double Scaling, double OriginX, double OriginY)
{
    public static PixelGrid Unscaled { get; } = new(1, 0, 0);

    /// <summary>The size of one device pixel in units of the control.</summary>
    public double PixelSize => 1 / Scaling;

    /// <summary>Moves a horizontal position to the nearest pixel boundary.</summary>
    public double SnapX(double x) => Snap(x, OriginX);

    /// <summary>Moves a vertical position to the nearest pixel boundary.</summary>
    public double SnapY(double y) => Snap(y, OriginY);

    /// <summary>Rounds a length to whole device pixels, but to no fewer than <paramref name="minimumPixels"/>.</summary>
    public double RoundLength(double length, int minimumPixels = 0) => Math.Max(minimumPixels, RoundHalfUp(length * Scaling)) / Scaling;

    private double Snap(double value, double origin) => (RoundHalfUp(value * Scaling + origin) - origin) / Scaling;

    /// <summary>Rounds halves up rather than to even, so shapes a whole number of pixels apart snap alike.</summary>
    private static double RoundHalfUp(double value) => Math.Floor(value + 0.5);
}
