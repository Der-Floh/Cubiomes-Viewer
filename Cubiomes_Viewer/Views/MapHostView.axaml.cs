using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Extensions;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

public sealed partial class MapHostView : UserControl, IMapImageSource
{
    private const double PinDragSlop = 4;

    private readonly MapViewModel _map;
    private readonly MapMenuViewModel _menu;
    private readonly SelectionViewModel _selection;
    private readonly MapLayerSet _layers;
    private readonly IMapStyleService _styleService;

    private PinDrag? _pinDrag;

    private Cursor PlacingCursor => field ??= new Cursor(StandardCursorType.Cross);
    private Cursor MovingCursor => field ??= new Cursor(StandardCursorType.SizeAll);

    public MapHostView(MapHostViewModel viewModel, MapLayerSet layers, IMapStyleService styleService)
    {
        InitializeComponent();
        DataContext = viewModel;

        _map = viewModel.Map;
        _menu = viewModel.Menu;
        _selection = viewModel.Selection;
        _layers = layers;
        _styleService = styleService;

        foreach (var layer in layers.InDrawOrder)
            Map.AddLayer(layer);
    }

    public byte[] CapturePng()
    {
        var scaling = Map.GetPixelGrid().Scaling;
        var size = new PixelSize((int)Math.Ceiling(Map.Bounds.Width * scaling), (int)Math.Ceiling(Map.Bounds.Height * scaling));
        if (size.Width <= 0 || size.Height <= 0)
            throw new InvalidOperationException("The map is not visible, so there is nothing to save.");

        using var bitmap = new RenderTargetBitmap(size, new Vector(Constants.ScreenDpi * scaling, Constants.ScreenDpi * scaling));
        bitmap.Render(Map);

        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Map.HoverChanged += OnHoverChanged;
        Map.Clicked += OnMapClicked;
        Map.HeightStepped += OnHeightStepped;
        Map.SizeChanged += OnMapSizeChanged;
        Map.PropertyChanged += OnMapViewPropertyChanged;
        Callout.SizeChanged += OnCalloutSizeChanged;
        // The context flyout handles the bubbling event before other handlers see it, so listen while it tunnels.
        Map.AddHandler(ContextRequestedEvent, OnContextRequested, RoutingStrategies.Tunnel);
        Map.AddHandler(PointerPressedEvent, OnMapPointerPressed, RoutingStrategies.Tunnel);
        Map.AddHandler(PointerMovedEvent, OnMapPointerMoved, RoutingStrategies.Tunnel);
        Map.AddHandler(PointerReleasedEvent, OnMapPointerReleased, RoutingStrategies.Tunnel);
        Map.PointerCaptureLost += OnMapPointerCaptureLost;
        _map.NavigationRequested += OnNavigationRequested;
        _map.PropertyChanged += OnMapPropertyChanged;
        _selection.PropertyChanged += OnSelectionPropertyChanged;
        _menu.ImageSource = this;

        ShowOverlays();
        ShowMeasurement();
        ShowSelection();
        ShowPinPlacing();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        Map.HoverChanged -= OnHoverChanged;
        Map.Clicked -= OnMapClicked;
        Map.HeightStepped -= OnHeightStepped;
        Map.SizeChanged -= OnMapSizeChanged;
        Map.PropertyChanged -= OnMapViewPropertyChanged;
        Callout.SizeChanged -= OnCalloutSizeChanged;
        Map.RemoveHandler(ContextRequestedEvent, OnContextRequested);
        Map.RemoveHandler(PointerPressedEvent, OnMapPointerPressed);
        Map.RemoveHandler(PointerMovedEvent, OnMapPointerMoved);
        Map.RemoveHandler(PointerReleasedEvent, OnMapPointerReleased);
        Map.PointerCaptureLost -= OnMapPointerCaptureLost;
        _pinDrag = null;
        _map.NavigationRequested -= OnNavigationRequested;
        _map.PropertyChanged -= OnMapPropertyChanged;
        _selection.PropertyChanged -= OnSelectionPropertyChanged;
        _menu.ImageSource = null;
    }

    /// <summary>Delete or Backspace takes the picked pin or measurement away, as its trash button does, unless a text box has the keys.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Delete or Key.Back) || IsInTextBox(e.Source) || !_selection.RemoveCommand.CanExecute(null))
            return;

        _selection.RemoveCommand.Execute(null);
        e.Handled = true;
    }

    private static bool IsInTextBox(object? source) => source is Visual visual && visual.FindAncestorOfType<TextBox>(includeSelf: true) is not null;

    private void OnHoverChanged(object? sender, MapHoverEventArgs e) => _map.SetHover(e.Position);

    private void OnHeightStepped(object? sender, MapHeightStepEventArgs e) => _map.StepSampleHeight(e.Steps);

    private void OnMapSizeChanged(object? sender, SizeChangedEventArgs e) => _map.ViewSize = (e.NewSize.Width, e.NewSize.Height);

    private void OnMapViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MapView.CameraProperty || e.Property == BoundsProperty)
            PlaceCallout();
    }

    private void OnCalloutSizeChanged(object? sender, SizeChangedEventArgs e) => PlaceCallout();

    private void OnMapClicked(object? sender, MapClickEventArgs e)
    {
        if (_map.IsMeasuring)
            _map.FinishMeasuring(e.Block);
        else if (_map.IsPlacingPin)
            _map.PlacePin(e.Block);
        else
            _map.Selection = Map.HitTestMarker(e.Position);

        if (e.PointerType != PointerType.Mouse)
            _map.SetHover(e.Block);
    }

    /// <summary>Takes a press on the picked pin before the map would start panning, so that dragging moves the pin.</summary>
    private void OnMapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var position = e.GetPosition(Map);
        if (_map.Selection?.Pin is not { } pin || !e.GetCurrentPoint(Map).Properties.IsLeftButtonPressed || !IsOverPickedPin(position))
            return;

        _pinDrag = new PinDrag(pin, e.Pointer, position, PinLayer.GetTip(Map.Viewport, pin) - position);
        e.Pointer.Capture(Map);
        Map.Focus();
        e.Handled = true;
    }

    private void OnMapPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(Map);
        if (_pinDrag is not { } drag || e.Pointer != drag.Pointer)
        {
            if (_pinDrag is null && !_map.IsPlacingPin)
                Map.Cursor = IsOverPickedPin(position) ? MovingCursor : null;
            return;
        }

        e.Handled = true;
        if (!drag.IsMoving && ((Vector)(position - drag.Start)).Length < PinDragSlop)
            return;

        drag.IsMoving = true;
        Map.Cursor = MovingCursor;
        var block = Map.GetBlockAt(position + drag.TipOffset);
        if (block != new BlockPos(drag.Pin.X, drag.Pin.Z))
            _map.MovePin(drag.Pin, block);
    }

    private void OnMapPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pinDrag is not { } drag || e.Pointer != drag.Pointer)
            return;

        e.Handled = true;
        _pinDrag = null;
        e.Pointer.Capture(null);
        ShowPinPlacing();
    }

    private void OnMapPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_pinDrag?.Pointer != e.Pointer)
            return;

        _pinDrag = null;
        ShowPinPlacing();
    }

    private bool IsOverPickedPin(Point position)
        => _map.Selection is { Pin: not null } picked && _layers.Selection.GetMarkerBounds(Map.Viewport, Map.GetPixelGrid(), picked).Contains(position);

    /// <summary>Points the menu at the block under the pointer; while a distance is measured, a right click ends the measurement instead, as Escape does.</summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_map.IsMeasuring)
        {
            _menu.StopMeasuring();
            e.Handled = true;
            return;
        }

        var position = e.TryGetPosition(Map, out var pointer) ? pointer : new Point(Map.Bounds.Width / 2, Map.Bounds.Height / 2);
        _menu.Position = Map.GetBlockAt(position);
    }

    private void OnNavigationRequested(MapNavigation navigation) => Map.NavigateTo(navigation.Target, navigation.Animate);

    private void OnMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MapViewModel.Layer) or nameof(MapViewModel.HeightStyle) or nameof(MapViewModel.PotentialStructure) or nameof(MapViewModel.BiomeColors)
                or nameof(MapViewModel.ShowGrid) or nameof(MapViewModel.ShowSlimeChunks) or nameof(MapViewModel.ShowGatewayLinks)
                or nameof(MapViewModel.ShowSpawn) or nameof(MapViewModel.ShowStrongholds) or nameof(MapViewModel.VisibleStructures) or nameof(MapViewModel.HiddenStructureOptions)
                or nameof(MapViewModel.GridSpacing) or nameof(MapViewModel.GridMultiplier) or nameof(MapViewModel.MarkerScale) or nameof(MapViewModel.MarkerZoomLimits):
                ShowOverlays();
                Map.Refresh();
                PlaceCallout();
                break;
            case nameof(MapViewModel.MeasureStart) or nameof(MapViewModel.HoverPosition):
                ShowMeasurement();
                break;
            case nameof(MapViewModel.Selection):
                ShowSelection();
                break;
            case nameof(MapViewModel.IsPlacingPin):
                ShowPinPlacing();
                break;
        }
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectionViewModel.Boxes))
            ShowSelection();
    }

    private void ShowOverlays()
    {
        _layers.Tiles.Style = new TileStyle(_map.HeightStyle, _map.Layer == LayerMode.StructurePotential ? _map.PotentialStructure : null, _map.BiomeColors);
        _layers.Grid.IsVisible = _map.ShowGrid;
        _layers.Grid.BaseSpacing = _map.GridSpacing;
        _layers.Grid.Multiplier = _map.GridMultiplier;
        _styleService.MarkerScale = _map.MarkerScale;
        _layers.SlimeChunks.IsVisible = _map.ShowSlimeChunks;
        _layers.Gateways.IsVisible = _map.ShowGatewayLinks;
        _layers.Landmarks.ShowSpawn = _map.ShowSpawn;
        _layers.Landmarks.ShowStrongholds = _map.ShowStrongholds;
        _layers.Structures.VisibleTypes = _map.VisibleStructures;
        _layers.Structures.ZoomLimits = _map.MarkerZoomLimits;
        _layers.Structures.HiddenOptions = _map.HiddenStructureOptions;
    }

    private void ShowPinPlacing() => Map.Cursor = _map.IsPlacingPin ? PlacingCursor : null;

    private void ShowMeasurement()
    {
        _layers.Measure.Start = _map.MeasureStart;
        _layers.Measure.End = _map.HoverPosition;
    }

    private void ShowSelection()
    {
        _layers.Selection.Picked = _map.Selection;
        _layers.Selection.Boxes = _selection.Boxes;
        PlaceCallout();
    }

    /// <summary>Keeps the callout of the picked marker beside the marker while the map moves under it.</summary>
    private void PlaceCallout()
    {
        if (_map.Selection is not { } picked)
            return;

        Callout.Measure(Size.Infinity);
        var marker = _layers.Selection.GetMarkerBounds(Map.Viewport, Map.GetPixelGrid(), picked);
        var position = CalloutPlacement.Place(Callout.DesiredSize, marker, Map.Bounds.Size);
        Canvas.SetLeft(Callout, position.X);
        Canvas.SetTop(Callout, position.Y);
    }

    /// <summary>A press on the picked pin: where it began and how far the tip of the pin lies from the pointer.</summary>
    private sealed class PinDrag(MapPin pin, IPointer pointer, Point start, Vector tipOffset)
    {
        public MapPin Pin { get; } = pin;
        public IPointer Pointer { get; } = pointer;
        public Point Start { get; } = start;
        public Vector TipOffset { get; } = tipOffset;
        public bool IsMoving { get; set; }
    }
}
