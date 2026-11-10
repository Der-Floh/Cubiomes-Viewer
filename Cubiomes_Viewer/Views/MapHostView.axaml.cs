using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private readonly GridLayer _gridLayer;
    private readonly SlimeChunkLayer _slimeChunkLayer;
    private readonly StructureLayer _structureLayer;
    private readonly LandmarkLayer _landmarkLayer;
    private readonly MeasureLayer _measureLayer;

    public MapHostView(MapHostViewModel viewModel, TileLayer tileLayer, SlimeChunkLayer slimeChunkLayer, GridLayer gridLayer, StructureLayer structureLayer, LandmarkLayer landmarkLayer, MeasureLayer measureLayer)
    {
        InitializeComponent();
        DataContext = viewModel;

        _map = viewModel.Map;
        _menu = viewModel.Menu;
        _gridLayer = gridLayer;
        _slimeChunkLayer = slimeChunkLayer;
        _structureLayer = structureLayer;
        _landmarkLayer = landmarkLayer;
        _measureLayer = measureLayer;
        ShowOverlays();
        ShowMeasurement();

        Map.AddLayer(tileLayer);
        Map.AddLayer(slimeChunkLayer);
        Map.AddLayer(gridLayer);
        Map.AddLayer(structureLayer);
        Map.AddLayer(landmarkLayer);
        Map.AddLayer(measureLayer);
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
        Map.ContextRequested += OnContextRequested;
        _map.NavigationRequested += OnNavigationRequested;
        _map.PropertyChanged += OnMapPropertyChanged;
        _menu.ImageSource = this;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        Map.HoverChanged -= OnHoverChanged;
        Map.ContextRequested -= OnContextRequested;
        _map.NavigationRequested -= OnNavigationRequested;
        _map.PropertyChanged -= OnMapPropertyChanged;
        _menu.ImageSource = null;
    }

    private void OnHoverChanged(object? sender, MapHoverEventArgs e) => _map.SetHover(e.Position);

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
            case nameof(MapViewModel.ShowGrid) or nameof(MapViewModel.ShowSlimeChunks) or nameof(MapViewModel.ShowSpawn)
                or nameof(MapViewModel.ShowStrongholds) or nameof(MapViewModel.VisibleStructures):
                ShowOverlays();
                Map.Refresh();
                break;
            case nameof(MapViewModel.MeasureStart) or nameof(MapViewModel.HoverPosition):
                ShowMeasurement();
                break;
        }
    }

    private void ShowOverlays()
    {
        _gridLayer.IsVisible = _map.ShowGrid;
        _slimeChunkLayer.IsVisible = _map.ShowSlimeChunks;
        _landmarkLayer.ShowSpawn = _map.ShowSpawn;
        _landmarkLayer.ShowStrongholds = _map.ShowStrongholds;
        _structureLayer.VisibleTypes = _map.VisibleStructures;
    }

    private void ShowMeasurement()
    {
        _measureLayer.Start = _map.MeasureStart;
        _measureLayer.End = _map.HoverPosition;
    }
}
