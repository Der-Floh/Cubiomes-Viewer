using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Tests.Data;

public sealed class PixelGridTests
{
    private const int Precision = 9;

    [Fact]
    public void Snap_AtAFractionalScaleAndOrigin_LandsOnADevicePixelBoundary()
    {
        var pixels = new PixelGrid(1.25, 0.5, 0.25);

        var x = pixels.SnapX(10.3);
        var y = pixels.SnapY(10.3);

        Assert.Equal(10, x, Precision);
        Assert.Equal(13, x * pixels.Scaling + pixels.OriginX, Precision);
        Assert.Equal(13, y * pixels.Scaling + pixels.OriginY, Precision);
    }

    [Fact]
    public void Snap_Halves_RoundUpWhetherThePixelIsEvenOrOdd()
    {
        Assert.Equal((11.0, 14.0), (PixelGrid.Unscaled.SnapX(10.5), PixelGrid.Unscaled.SnapY(13.5)));
    }

    [Fact]
    public void RoundLength_ToWholePixels_KeepsTheMinimum()
    {
        var pixels = new PixelGrid(1.5, 0, 0);

        Assert.Equal(2 / 1.5, pixels.RoundLength(1), Precision);
        Assert.Equal(0, pixels.RoundLength(0.2), Precision);
        Assert.Equal(1 / 1.5, pixels.RoundLength(0.2, minimumPixels: 1), Precision);
    }
}
