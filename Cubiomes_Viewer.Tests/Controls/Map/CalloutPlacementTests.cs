using Avalonia;

using Cubiomes_Viewer.Controls.Map;

namespace Cubiomes_Viewer.Tests.Controls.Map;

public sealed class CalloutPlacementTests
{
    private static readonly Size _view = new(400, 300);
    private static readonly Size _callout = new(120, 60);

    [Fact]
    public void Place_MarkerWithRoomAbove_CentresTheCalloutAboveIt()
    {
        var position = CalloutPlacement.Place(_callout, new Rect(190, 140, 20, 20), _view);

        Assert.Equal(new Point(140, 140 - CalloutPlacement.Gap - _callout.Height), position);
    }

    [Fact]
    public void KeepTop_CalloutGrownWhileItFits_KeepsItsTopEdge()
    {
        var grown = new Size(_callout.Width, 200);

        var position = CalloutPlacement.KeepTop(40, grown, new Rect(190, 140, 20, 20), _view);

        Assert.Equal(new Point(140, 40), position);
    }

    [Fact]
    public void KeepTop_CalloutGrownPastTheBottom_IsPlacedAsUsual()
    {
        var grown = new Size(_callout.Width, 200);
        var marker = new Rect(190, 240, 20, 20);

        Assert.Equal(CalloutPlacement.Place(grown, marker, _view), CalloutPlacement.KeepTop(150, grown, marker, _view));
    }

    [Fact]
    public void KeepTop_CalloutNotPlacedYet_IsPlacedAsUsual()
    {
        var marker = new Rect(190, 140, 20, 20);

        Assert.Equal(CalloutPlacement.Place(_callout, marker, _view), CalloutPlacement.KeepTop(double.NaN, _callout, marker, _view));
    }

    [Fact]
    public void Place_MarkerNearTheTop_PutsTheCalloutBelowIt()
    {
        var position = CalloutPlacement.Place(_callout, new Rect(190, 20, 20, 20), _view);

        Assert.Equal(40 + CalloutPlacement.Gap, position.Y);
    }

    [Fact]
    public void Place_MarkerNearASide_KeepsTheCalloutInsideTheMap()
    {
        var position = CalloutPlacement.Place(_callout, new Rect(360, 140, 20, 20), _view);

        Assert.Equal(_view.Width - CalloutPlacement.Margin - _callout.Width, position.X);
    }

    [Fact]
    public void Place_CalloutTooTallForEitherSide_KeepsItsTopInTheMapOverTheMarker()
    {
        var tall = new Size(_callout.Width, 250);

        var position = CalloutPlacement.Place(tall, new Rect(190, 140, 20, 20), _view);

        Assert.Equal(CalloutPlacement.Margin, position.Y);
    }

    [Fact]
    public void Place_TallCalloutOfAMarkerLeavingThroughTheTop_LeavesWithIt()
    {
        var tall = new Size(_callout.Width, 320);

        var position = CalloutPlacement.Place(tall, new Rect(190, -40, 20, 20), _view);

        Assert.Equal(-30 - CalloutPlacement.MinOverlap, position.Y);
    }

    [Fact]
    public void Place_MarkerLeavingTheMap_TakesTheCalloutAlong()
    {
        var position = CalloutPlacement.Place(_callout, new Rect(-60, 140, 20, 20), _view);

        Assert.Equal(-50 - CalloutPlacement.MinOverlap, position.X);
    }
}
