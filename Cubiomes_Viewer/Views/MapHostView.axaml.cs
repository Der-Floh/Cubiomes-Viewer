using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

public sealed partial class MapHostView : UserControl
{
    private readonly MapViewModel _viewModel;
    private readonly GridLayer _gridLayer;
    private readonly SlimeChunkLayer _slimeChunkLayer;
    private readonly StructureLayer _structureLayer;
    private readonly LandmarkLayer _landmarkLayer;

    public MapHostView(MapViewModel viewModel, TileLayer tileLayer, SlimeChunkLayer slimeChunkLayer, GridLayer gridLayer, StructureLayer structureLayer, LandmarkLayer landmarkLayer)
    {
        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _gridLayer = gridLayer;
        _slimeChunkLayer = slimeChunkLayer;
        _structureLayer = structureLayer;
        _landmarkLayer = landmarkLayer;
        ShowOverlays();

        Map.AddLayer(tileLayer);
        Map.AddLayer(slimeChunkLayer);
        Map.AddLayer(gridLayer);
        Map.AddLayer(structureLayer);
        Map.AddLayer(landmarkLayer);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Map.HoverChanged += OnHoverChanged;
        _viewModel.NavigationRequested += OnNavigationRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        Map.HoverChanged -= OnHoverChanged;
        _viewModel.NavigationRequested -= OnNavigationRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnHoverChanged(object? sender, MapHoverEventArgs e) => _viewModel.SetHover(e.Position);

    private void OnNavigationRequested(MapCamera target) => Map.NavigateTo(target, animate: true);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(MapViewModel.ShowGrid) or nameof(MapViewModel.ShowSlimeChunks) or nameof(MapViewModel.ShowSpawn)
            or nameof(MapViewModel.ShowStrongholds) or nameof(MapViewModel.VisibleStructures)))
        {
            return;
        }

        ShowOverlays();
        Map.Refresh();
    }

    private void ShowOverlays()
    {
        _gridLayer.IsVisible = _viewModel.ShowGrid;
        _slimeChunkLayer.IsVisible = _viewModel.ShowSlimeChunks;
        _landmarkLayer.ShowSpawn = _viewModel.ShowSpawn;
        _landmarkLayer.ShowStrongholds = _viewModel.ShowStrongholds;
        _structureLayer.VisibleTypes = _viewModel.VisibleStructures;
    }
}
