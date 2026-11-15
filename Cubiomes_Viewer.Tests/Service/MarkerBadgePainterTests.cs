using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class MarkerBadgePainterTests
{
    private const int Precision = 6;

    [Theory]
    [InlineData(22, 1, 22)]
    [InlineData(16.5, 1, 18)]
    [InlineData(29.7, 1, 36)]
    [InlineData(33, 1, 36)]
    [InlineData(49.5, 1, 54)]
    [InlineData(22, 1.25, 22)]
    [InlineData(22, 1.5, 36)]
    [InlineData(22, 2, 44)]
    public void GetSide_OfAnySize_IsTheNearestSideAGlyphOnWholeDevicePixelsAllows(double side, double scaling, int devicePixels)
        => Assert.Equal(devicePixels, MarkerBadgePainter.GetSide(side, new PixelGrid(scaling, 0, 0)) * scaling, Precision);

    [Fact]
    public void GetSide_WithAMaximum_StaysWithinIt()
        => Assert.Equal(20, MarkerBadgePainter.GetSide(22, PixelGrid.Unscaled, maximumSide: 20), Precision);
}
