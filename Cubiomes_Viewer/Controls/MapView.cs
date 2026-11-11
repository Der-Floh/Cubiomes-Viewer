using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls;

/// <summary>
/// The pannable, zoomable map. It owns input and animation and delegates all drawing to its <see cref="IMapLayer"/>s.
/// </summary>
public sealed class MapView : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<MapView>();

    public static readonly StyledProperty<MapCamera> CameraProperty =
        AvaloniaProperty.Register<MapView, MapCamera>(nameof(Camera), MapCamera.Default, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<WorldKey> WorldProperty = AvaloniaProperty.Register<MapView, WorldKey>(nameof(World));

    public static readonly StyledProperty<int> SampleHeightProperty = AvaloniaProperty.Register<MapView, int>(nameof(SampleHeight));

    public static readonly StyledProperty<bool> SmoothMotionProperty = AvaloniaProperty.Register<MapView, bool>(nameof(SmoothMotion), true);

    private const double TapSlopPixels = 6;

    private static readonly TimeSpan _navigationDuration = TimeSpan.FromMilliseconds(450);

    public event EventHandler<MapHoverEventArgs>? HoverChanged;

    /// <summary>Raised when the map is clicked or tapped without being dragged.</summary>
    public event EventHandler<MapClickEventArgs>? Clicked;

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public MapCamera Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    public WorldKey World
    {
        get => GetValue(WorldProperty);
        set => SetValue(WorldProperty, value);
    }

    public int SampleHeight
    {
        get => GetValue(SampleHeightProperty);
        set => SetValue(SampleHeightProperty, value);
    }

    public bool SmoothMotion
    {
        get => GetValue(SmoothMotionProperty);
        set => SetValue(SmoothMotionProperty, value);
    }

    private readonly List<IMapLayer> _layers = [];
    private readonly MapGestureController _gestures = new();
    private readonly Dictionary<int, Point> _pointers = [];
    private Point? _hoverPosition;
    private Point? _tapStart;
    private Point _pinchCenter;
    private double _pinchDistance;
    private Navigation? _navigation;
    private TimeSpan? _lastFrameTime;
    private bool _isFrameRequested;
    private bool _isAttached;

    static MapView()
    {
        AffectsRender<MapView>(BackgroundProperty);
    }

    public MapView()
    {
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public void AddLayer(IMapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        _layers.Add(layer);
        if (_isAttached)
            AttachLayer(layer);
    }

    /// <summary>Redraws the map, for example after a layer's visibility changed.</summary>
    public void Refresh()
    {
        UpdateLayers();
        InvalidateVisual();
    }

    public void NavigateTo(MapCamera target, bool animate)
    {
        var clamped = MapGestureController.Clamp(target, ZoomLimits);
        _gestures.CancelDrag();

        if (!animate || !SmoothMotion || !_isAttached)
        {
            _navigation = null;
            SetCurrentValue(CameraProperty, clamped);
            return;
        }

        _navigation = new Navigation(Camera, clamped);
        RequestFrame();
    }

    /// <summary>Returns the block shown at a position inside the control.</summary>
    public BlockPos GetBlockAt(Point position)
    {
        var (blockX, blockZ) = Viewport.ToWorld(position.X, position.Y);
        return new BlockPos((int)Math.Floor(blockX), (int)Math.Floor(blockZ));
    }

    /// <summary>Returns the topmost marker drawn at a position inside the control, or <see langword="null"/>.</summary>
    public MapSelection? HitTestMarker(Point position)
    {
        var scene = CreateScene();
        for (var index = _layers.Count - 1; index >= 0; index--)
        {
            if (_layers[index] is IMarkerLayer markers && markers.HitTest(scene, position) is { } hit)
                return hit;
        }

        return null;
    }

    public override void Render(DrawingContext context)
    {
        if (Background is { } background)
            context.FillRectangle(background, new Rect(Bounds.Size));

        var scene = CreateScene();
        foreach (var layer in _layers)
            layer.Render(context, scene);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _isAttached = true;
        foreach (var layer in _layers)
            AttachLayer(layer);
        UpdateLayers();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _isAttached = false;
        _navigation = null;
        _gestures.CancelDrag();
        _pointers.Clear();
        foreach (var layer in _layers)
        {
            layer.Invalidated -= OnLayerInvalidated;
            layer.Detach();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WorldProperty)
            SetCurrentValue(CameraProperty, MapGestureController.Clamp(Camera, ZoomLimits));

        if (change.Property == CameraProperty || change.Property == WorldProperty || change.Property == SampleHeightProperty || change.Property == BoundsProperty)
        {
            Refresh();
            if (_hoverPosition is { } position)
                RaiseHover(position);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
            return;

        Focus();
        e.Pointer.Capture(this);
        _navigation = null;
        _pointers[e.Pointer.Id] = point.Position;

        if (_pointers.Count == 1)
        {
            _tapStart = point.Position;
            _gestures.BeginDrag(point.Position.X, point.Position.Y, GetTime(e));
        }
        else
        {
            _tapStart = null;
            BeginPinch();
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);
        if (_pointers.ContainsKey(e.Pointer.Id))
        {
            _pointers[e.Pointer.Id] = position;
            if (_tapStart is { } start && Point.Distance(start, position) > TapSlopPixels)
                _tapStart = null;
            if (_pointers.Count == 1)
                SetCurrentValue(CameraProperty, _gestures.Drag(Camera, position.X, position.Y, GetTime(e)));
            else
                UpdatePinch();
        }

        if (e.Pointer.Type == PointerType.Mouse)
            RaiseHover(position);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_pointers.Remove(e.Pointer.Id))
            return;

        e.Pointer.Capture(null);
        if (_pointers.Count == 0)
        {
            RaiseClickIfTapped(e);
            _gestures.EndDrag(GetTime(e));
            if (SmoothMotion && _gestures.HasInertia)
                RequestFrame();
            else
                _gestures.StopInertia();
        }
        else if (_pointers.Count == 1)
        {
            var remaining = _pointers.Values.First();
            _gestures.BeginDrag(remaining.X, remaining.Y, GetTime(e));
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        if (!_pointers.Remove(e.Pointer.Id))
            return;

        _tapStart = null;
        if (_pointers.Count == 0)
            _gestures.CancelDrag();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type != PointerType.Mouse)
            return;

        _hoverPosition = null;
        HoverChanged?.Invoke(this, new MapHoverEventArgs(null));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var position = e.GetPosition(this);
        _navigation = null;
        _gestures.StopInertia();
        SetCurrentValue(CameraProperty, MapGestureController.ZoomAt(
            Viewport, position.X, position.Y, Math.Pow(MapGestureController.WheelZoomStep, e.Delta.Y), ZoomLimits));
        RaiseHover(position);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        const double pan = MapGestureController.KeyboardPanPixels;
        const double zoom = MapGestureController.KeyboardZoomStep;
        MapCamera? camera = e.Key switch
        {
            Key.Left => MapGestureController.Pan(Camera, pan, 0),
            Key.Right => MapGestureController.Pan(Camera, -pan, 0),
            Key.Up => MapGestureController.Pan(Camera, 0, pan),
            Key.Down => MapGestureController.Pan(Camera, 0, -pan),
            Key.Add or Key.OemPlus => MapGestureController.ZoomAt(Viewport, Bounds.Width / 2, Bounds.Height / 2, zoom, ZoomLimits),
            Key.Subtract or Key.OemMinus => MapGestureController.ZoomAt(Viewport, Bounds.Width / 2, Bounds.Height / 2, 1 / zoom, ZoomLimits),
            Key.Home => Camera with { CenterX = 0, CenterZ = 0 },
            _ => null,
        };

        if (camera is not { } target)
            return;

        _navigation = null;
        _gestures.StopInertia();
        SetCurrentValue(CameraProperty, target);
        e.Handled = true;
    }

    private MapViewport Viewport => new(Camera, Bounds.Width, Bounds.Height);

    private MapZoomLimits ZoomLimits => new(TileMetrics.MinBlocksPerPixel, TileMetrics.For(World).MaxBlocksPerPixel);

    private MapScene CreateScene()
        => new(World, LayerMode.Biomes, SampleHeight, Viewport, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);

    private void AttachLayer(IMapLayer layer)
    {
        layer.Attach();
        layer.Invalidated += OnLayerInvalidated;
    }

    private void UpdateLayers()
    {
        if (!_isAttached)
            return;

        var scene = CreateScene();
        foreach (var layer in _layers)
            layer.UpdateScene(scene);
    }

    private void OnLayerInvalidated(object? sender, EventArgs e) => InvalidateVisual();

    private void RaiseClickIfTapped(PointerReleasedEventArgs e)
    {
        if (_tapStart is null)
            return;

        _tapStart = null;
        var position = e.GetPosition(this);
        Clicked?.Invoke(this, new MapClickEventArgs(position, GetBlockAt(position), e.Pointer.Type));
    }

    private void RaiseHover(Point position)
    {
        _hoverPosition = position;
        HoverChanged?.Invoke(this, new MapHoverEventArgs(GetBlockAt(position)));
    }

    private void BeginPinch()
    {
        _gestures.CancelDrag();
        (_pinchCenter, _pinchDistance) = GetPinchGeometry();
    }

    private void UpdatePinch()
    {
        var (center, distance) = GetPinchGeometry();
        if (_pinchDistance > 0 && distance > 0)
        {
            var panned = MapGestureController.Pan(Camera, center.X - _pinchCenter.X, center.Y - _pinchCenter.Y);
            var viewport = new MapViewport(panned, Bounds.Width, Bounds.Height);
            SetCurrentValue(CameraProperty, MapGestureController.ZoomAt(viewport, center.X, center.Y, distance / _pinchDistance, ZoomLimits));
        }

        _pinchCenter = center;
        _pinchDistance = distance;
    }

    private (Point Center, double Distance) GetPinchGeometry()
    {
        var first = _pointers.Values.First();
        var second = _pointers.Values.Skip(1).First();
        var delta = second - first;
        return (first + delta / 2, Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y));
    }

    private void RequestFrame()
    {
        if (_isFrameRequested || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        _isFrameRequested = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan time)
    {
        _isFrameRequested = false;
        var elapsed = _lastFrameTime is { } last ? time - last : TimeSpan.Zero;
        _lastFrameTime = time;

        var isActive = _navigation is not null ? StepNavigation(elapsed) : StepInertia(elapsed);
        if (isActive && _isAttached)
            RequestFrame();
        else
            _lastFrameTime = null;
    }

    private bool StepNavigation(TimeSpan elapsed)
    {
        if (_navigation is not { } navigation)
            return false;

        navigation.Elapsed += elapsed;
        var progress = Math.Clamp(navigation.Elapsed / _navigationDuration, 0, 1);
        SetCurrentValue(CameraProperty, navigation.GetCamera(progress));

        if (progress < 1)
            return true;

        _navigation = null;
        return false;
    }

    private bool StepInertia(TimeSpan elapsed)
    {
        if (!_gestures.HasInertia)
            return false;

        SetCurrentValue(CameraProperty, _gestures.StepInertia(Camera, elapsed));
        return _gestures.HasInertia;
    }

    private static TimeSpan GetTime(PointerEventArgs e) => TimeSpan.FromMilliseconds(e.Timestamp);

    private sealed class Navigation(MapCamera from, MapCamera to)
    {
        public TimeSpan Elapsed { get; set; }

        public MapCamera GetCamera(double progress)
        {
            var eased = progress * progress * (3 - 2 * progress);
            return new MapCamera(
                from.CenterX + (to.CenterX - from.CenterX) * eased,
                from.CenterZ + (to.CenterZ - from.CenterZ) * eased,
                from.BlocksPerPixel * Math.Pow(to.BlocksPerPixel / from.BlocksPerPixel, eased));
        }
    }
}
