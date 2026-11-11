using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Tests.Data;

public sealed class ColorRampTests
{
    private static readonly RgbColor _black = new(0, 0, 0);
    private static readonly RgbColor _red = new(200, 0, 0);
    private static readonly RgbColor _white = new(255, 255, 255);

    private readonly ColorRamp _ramp = new(new(-1, _black), new(0, _red), new(4, _white));

    [Fact]
    public void GetColor_AtAStop_ReturnsItsColour()
    {
        Assert.Equal(_black, _ramp.GetColor(-1));
        Assert.Equal(_red, _ramp.GetColor(0));
        Assert.Equal(_white, _ramp.GetColor(4));
    }

    [Fact]
    public void GetColor_BetweenTwoStops_BlendsThemByDistance()
    {
        Assert.Equal(new RgbColor(100, 0, 0), _ramp.GetColor(-0.5));
        Assert.Equal(new RgbColor(214, 64, 64), _ramp.GetColor(1));
    }

    [Fact]
    public void GetColor_OutsideTheRamp_ReturnsTheColourOfTheNearestEnd()
    {
        Assert.Equal(_black, _ramp.GetColor(-100));
        Assert.Equal(_white, _ramp.GetColor(100));
    }

    [Fact]
    public void MinimumAndMaximum_AreThePositionsOfTheEnds()
    {
        Assert.Equal(-1, _ramp.Minimum);
        Assert.Equal(4, _ramp.Maximum);
    }

    [Fact]
    public void Constructor_FewerThanTwoStops_IsRejected()
        => Assert.Throws<ArgumentException>(() => new ColorRamp(new ColorStop(0, _black)));

    [Fact]
    public void Constructor_StopsOutOfOrder_AreRejected()
        => Assert.Throws<ArgumentException>(() => new ColorRamp(new(1, _black), new(0, _white)));
}
