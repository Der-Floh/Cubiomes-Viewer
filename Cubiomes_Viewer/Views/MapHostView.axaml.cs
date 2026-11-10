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

    public MapHostView(MapViewModel viewModel, TileLayer tileLayer, GridLayer gridLayer)
    {
        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _gridLayer = gridLayer;
        _gridLayer.IsVisible = viewModel.ShowGrid;

        Map.AddLayer(tileLayer);
        Map.AddLayer(gridLayer);
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
        if (e.PropertyName != nameof(MapViewModel.ShowGrid))
            return;

        _gridLayer.IsVisible = _viewModel.ShowGrid;
        Map.Refresh();
    }
}
