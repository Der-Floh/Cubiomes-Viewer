using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Tests.Controls.Map;

public sealed class MapGestureControllerTests
{
    private const double Tolerance = 1e-9;
    private static readonly MapZoomLimits _limits = new(1.0 / 32, 4096);

    private readonly MapGestureController _controller = new();

    [Fact]
    public void Pan_PointerMovedRightAndDown_MovesCentreUpLeftInBlocks()
    {
        var camera = MapGestureController.Pan(new MapCamera(100, 200, 4), 10, 5);

        Assert.Equal(new MapCamera(60, 180, 4), camera);
    }

    [Fact]
    public void Pan_BeyondWorldBorder_ClampsTheCentre()
    {
        var camera = MapGestureController.Pan(new MapCamera(TilePyramid.WorldLimitBlocks, 0, 1), -500, 0);

        Assert.Equal(TilePyramid.WorldLimitBlocks, camera.CenterX);
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(0.5)]
    [InlineData(1.25)]
    public void ZoomAt_AnyFactor_KeepsTheBlockUnderThePointerInPlace(double factor)
    {
        var viewport = new MapViewport(new MapCamera(1000, -500, 4), 800, 600);
        var before = viewport.ToWorld(650, 120);

        var zoomed = new MapViewport(MapGestureController.ZoomAt(viewport, 650, 120, factor, _limits), 800, 600);
        var after = zoomed.ToWorld(650, 120);

        Assert.Equal(4 / factor, zoomed.BlocksPerPixel, Tolerance);
        Assert.Equal(before.X, after.X, 1e-6);
        Assert.Equal(before.Z, after.Z, 1e-6);
    }

    [Fact]
    public void ZoomAt_PastTheLimits_StopsAtTheLimit()
    {
        var viewport = new MapViewport(new MapCamera(0, 0, 4), 800, 600);

        var zoomedIn = MapGestureController.ZoomAt(viewport, 400, 300, 1e6, _limits);
        var zoomedOut = MapGestureController.ZoomAt(viewport, 400, 300, 1e-6, _limits);

        Assert.Equal(_limits.MinBlocksPerPixel, zoomedIn.BlocksPerPixel);
        Assert.Equal(_limits.MaxBlocksPerPixel, zoomedOut.BlocksPerPixel);
    }

    [Fact]
    public void Drag_PointerMoves_FollowsThePointer()
    {
        var camera = new MapCamera(0, 0, 2);
        _controller.BeginDrag(100, 100, TimeSpan.Zero);

        camera = _controller.Drag(camera, 130, 90, TimeSpan.FromMilliseconds(16));
        camera = _controller.Drag(camera, 150, 80, TimeSpan.FromMilliseconds(32));

        Assert.Equal(new MapCamera(-100, 40, 2), camera);
    }

    [Fact]
    public void EndDrag_WhileMovingFast_LeavesInertiaThatDecaysToRest()
    {
        var camera = new MapCamera(0, 0, 1);
        _controller.BeginDrag(0, 0, TimeSpan.Zero);
        for (var step = 1; step <= 5; step++)
            camera = _controller.Drag(camera, step * 20, 0, TimeSpan.FromMilliseconds(step * 16));
        _controller.EndDrag(TimeSpan.FromMilliseconds(82));
        var released = camera;

        Assert.True(_controller.HasInertia);
        for (var frame = 0; frame < 600 && _controller.HasInertia; frame++)
            camera = _controller.StepInertia(camera, TimeSpan.FromMilliseconds(16));

        Assert.False(_controller.HasInertia);
        Assert.True(camera.CenterX < released.CenterX);
    }

    [Fact]
    public void EndDrag_AfterThePointerRested_LeavesNoInertia()
    {
        _controller.BeginDrag(0, 0, TimeSpan.Zero);
        _controller.Drag(MapCamera.Default, 200, 0, TimeSpan.FromMilliseconds(16));

        _controller.EndDrag(TimeSpan.FromMilliseconds(500));

        Assert.False(_controller.HasInertia);
    }

    [Fact]
    public void CancelDrag_DuringDrag_StopsFollowingThePointer()
    {
        _controller.BeginDrag(0, 0, TimeSpan.Zero);
        _controller.CancelDrag();

        var camera = _controller.Drag(MapCamera.Default, 300, 300, TimeSpan.FromMilliseconds(16));

        Assert.Equal(MapCamera.Default, camera);
        Assert.False(_controller.IsDragging);
    }
}
