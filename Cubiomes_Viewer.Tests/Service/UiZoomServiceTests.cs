using Cubiomes_Viewer.Service.UiZoomServ;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class UiZoomServiceTests
{
    [Fact]
    public void Apply_NewSize_ScalesAndTellsOnce()
    {
        var zoom = new UiZoomService();
        var changes = 0;
        zoom.Changed += (_, _) => changes++;

        zoom.Apply(150);
        zoom.Apply(150);

        Assert.Equal(1.5, zoom.Scale);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Apply_NoSize_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiZoomService().Apply(0));
    }
}
