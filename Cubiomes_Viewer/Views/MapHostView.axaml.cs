using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

public sealed partial class MapHostView : UserControl, IMapImageSource
{
    private readonly MapViewModel _map;
    private readonly MapMenuViewModel _menu;
    private readonly SelectionViewModel _selection;
    private readonly MapLayerSet _layers;

    public MapHostView(MapHostViewModel viewModel, MapLayerSet layers)
    {
        InitializeComponent();
        DataContext = viewModel;

        _map = viewModel.Map;
        _menu = viewModel.Menu;
        _selection = viewModel.Selection;
        _layers = layers;

        foreach (var layer in layers.InDrawOrder)
            Map.AddLayer(layer);
    }

    public byte[] CapturePng()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
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
        Map.SizeChanged += OnMapSizeChanged;
        // The context flyout handles the bubbling event before other handlers see it, so listen while it tunnels.
        Map.AddHandler(ContextRequestedEvent, OnContextRequested, RoutingStrategies.Tunnel);
        _map.NavigationRequested += OnNavigationRequested;
        _map.PropertyChanged += OnMapPropertyChanged;
        _selection.PropertyChanged += OnSelectionPropertyChanged;
        _menu.ImageSource = this;

        ShowOverlays();
        ShowMeasurement();
        ShowSelection();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        Map.HoverChanged -= OnHoverChanged;
        Map.Clicked -= OnMapClicked;
        Map.SizeChanged -= OnMapSizeChanged;
        Map.RemoveHandler(ContextRequestedEvent, OnContextRequested);
        _map.NavigationRequested -= OnNavigationRequested;
        _map.PropertyChanged -= OnMapPropertyChanged;
        _selection.PropertyChanged -= OnSelectionPropertyChanged;
        _menu.ImageSource = null;
    }

    private void OnHoverChanged(object? sender, MapHoverEventArgs e) => _map.SetHover(e.Position);

    private void OnMapSizeChanged(object? sender, SizeChangedEventArgs e) => _map.ViewSize = (e.NewSize.Width, e.NewSize.Height);

    private void OnMapClicked(object? sender, MapClickEventArgs e)
    {
        _map.Selection = Map.HitTestMarker(e.Position);
        if (e.PointerType != PointerType.Mouse)
            _map.SetHover(e.Block);
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var position = e.TryGetPosition(Map, out var pointer) ? pointer : new Point(Map.Bounds.Width / 2, Map.Bounds.Height / 2);
        _menu.Position = Map.GetBlockAt(position);
    }

    private void OnNavigationRequested(MapCamera target) => Map.NavigateTo(target, animate: true);

    private void OnMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MapViewModel.Layer) or nameof(MapViewModel.HeightStyle) or nameof(MapViewModel.PotentialStructure)
                or nameof(MapViewModel.ShowGrid) or nameof(MapViewModel.ShowSlimeChunks) or nameof(MapViewModel.ShowGatewayLinks)
                or nameof(MapViewModel.ShowSpawn) or nameof(MapViewModel.ShowStrongholds) or nameof(MapViewModel.VisibleStructures):
                ShowOverlays();
                Map.Refresh();
                break;
            case nameof(MapViewModel.MeasureStart) or nameof(MapViewModel.HoverPosition):
                ShowMeasurement();
                break;
            case nameof(MapViewModel.Selection):
                ShowSelection();
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
        _layers.Tiles.Style = new TileStyle(_map.HeightStyle, _map.Layer == LayerMode.StructurePotential ? _map.PotentialStructure : null);
        _layers.Grid.IsVisible = _map.ShowGrid;
        _layers.SlimeChunks.IsVisible = _map.ShowSlimeChunks;
        _layers.Gateways.IsVisible = _map.ShowGatewayLinks;
        _layers.Landmarks.ShowSpawn = _map.ShowSpawn;
        _layers.Landmarks.ShowStrongholds = _map.ShowStrongholds;
        _layers.Structures.VisibleTypes = _map.VisibleStructures;
    }

    private void ShowMeasurement()
    {
        _layers.Measure.Start = _map.MeasureStart;
        _layers.Measure.End = _map.HoverPosition;
    }

    private void ShowSelection()
    {
        _layers.Selection.Position = _map.Selection?.Position;
        _layers.Selection.Boxes = _selection.Boxes;
    }
}
