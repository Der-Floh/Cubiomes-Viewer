using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Pan, zoom and inertia arithmetic for the map. Works on plain numbers so it can be tested without a control.
/// </summary>
public sealed class MapGestureController
{
    public const double WheelZoomStep = 1.25;
    public const double KeyboardZoomStep = 1.5;
    public const double KeyboardPanPixels = 96;

    private const double InertiaDecayPerSecond = 4.5;
    private const double MinInertiaPixelsPerSecond = 30;
    private const double MaxInertiaPixelsPerSecond = 3000;
    private static readonly TimeSpan _velocityWindow = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan _maxReleaseGap = TimeSpan.FromMilliseconds(60);

    public bool IsDragging { get; private set; }
    public bool HasInertia => _velocityX != 0 || _velocityY != 0;

    private readonly Queue<(TimeSpan Time, double X, double Y)> _samples = new();
    private double _lastX;
    private double _lastY;
    private double _velocityX;
    private double _velocityY;

    /// <summary>Moves the camera so that the map content follows a pointer moved by the given pixel delta.</summary>
    public static MapCamera Pan(MapCamera camera, double deltaPixelsX, double deltaPixelsY)
        => ClampCenter(camera with
        {
            CenterX = camera.CenterX - deltaPixelsX * camera.BlocksPerPixel,
            CenterZ = camera.CenterZ - deltaPixelsY * camera.BlocksPerPixel,
        });

    /// <summary>Zooms by <paramref name="factor"/> (above 1 zooms in) while keeping the block under the given pixel in place.</summary>
    public static MapCamera ZoomAt(MapViewport viewport, double pixelX, double pixelY, double factor, MapZoomLimits limits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factor);

        var (worldX, worldZ) = viewport.ToWorld(pixelX, pixelY);
        var blocksPerPixel = Math.Clamp(viewport.BlocksPerPixel / factor, limits.MinBlocksPerPixel, limits.MaxBlocksPerPixel);
        return ClampCenter(new MapCamera(
            worldX - (pixelX - viewport.Width / 2) * blocksPerPixel,
            worldZ - (pixelY - viewport.Height / 2) * blocksPerPixel,
            blocksPerPixel));
    }

    public static MapCamera Clamp(MapCamera camera, MapZoomLimits limits)
        => ClampCenter(camera with { BlocksPerPixel = Math.Clamp(camera.BlocksPerPixel, limits.MinBlocksPerPixel, limits.MaxBlocksPerPixel) });

    public void BeginDrag(double x, double y, TimeSpan time)
    {
        StopInertia();
        IsDragging = true;
        _lastX = x;
        _lastY = y;
        _samples.Clear();
        _samples.Enqueue((time, x, y));
    }

    public MapCamera Drag(MapCamera camera, double x, double y, TimeSpan time)
    {
        if (!IsDragging)
            return camera;

        var moved = Pan(camera, x - _lastX, y - _lastY);
        _lastX = x;
        _lastY = y;

        _samples.Enqueue((time, x, y));
        while (_samples.Count > 2 && time - _samples.Peek().Time > _velocityWindow)
            _samples.Dequeue();

        return moved;
    }

    /// <summary>Ends the drag and keeps the release velocity as inertia when the pointer was still moving.</summary>
    public void EndDrag(TimeSpan time)
    {
        if (!IsDragging)
            return;

        IsDragging = false;
        if (_samples.Count >= 2)
        {
            var first = _samples.Peek();
            var last = _samples.Last();
            var seconds = (last.Time - first.Time).TotalSeconds;
            if (seconds > 0 && time - last.Time <= _maxReleaseGap)
            {
                _velocityX = (last.X - first.X) / seconds;
                _velocityY = (last.Y - first.Y) / seconds;
                LimitInertia();
            }
        }

        _samples.Clear();
    }

    public void CancelDrag()
    {
        IsDragging = false;
        _samples.Clear();
        StopInertia();
    }

    public MapCamera StepInertia(MapCamera camera, TimeSpan elapsed)
    {
        if (!HasInertia)
            return camera;

        var seconds = elapsed.TotalSeconds;
        var moved = Pan(camera, _velocityX * seconds, _velocityY * seconds);

        var decay = Math.Exp(-InertiaDecayPerSecond * seconds);
        _velocityX *= decay;
        _velocityY *= decay;
        StopInertiaIfSlow();

        return moved;
    }

    public void StopInertia()
    {
        _velocityX = 0;
        _velocityY = 0;
    }

    private void StopInertiaIfSlow()
    {
        if (double.Hypot(_velocityX, _velocityY) < MinInertiaPixelsPerSecond)
            StopInertia();
    }

    private void LimitInertia()
    {
        var speed = double.Hypot(_velocityX, _velocityY);
        if (speed > MaxInertiaPixelsPerSecond)
        {
            _velocityX *= MaxInertiaPixelsPerSecond / speed;
            _velocityY *= MaxInertiaPixelsPerSecond / speed;
        }

        StopInertiaIfSlow();
    }

    private static MapCamera ClampCenter(MapCamera camera)
        => camera with
        {
            CenterX = Math.Clamp(camera.CenterX, -TilePyramid.WorldLimitBlocks, TilePyramid.WorldLimitBlocks),
            CenterZ = Math.Clamp(camera.CenterZ, -TilePyramid.WorldLimitBlocks, TilePyramid.WorldLimitBlocks),
        };
}
