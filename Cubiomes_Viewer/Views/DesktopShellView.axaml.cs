using Avalonia.Controls;
using Avalonia.Input;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

public sealed partial class DesktopShellView : UserControl
{
    /// <summary>The panel the shared map view is placed in while this shell is active.</summary>
    public Panel MapHost => MapSlot;

    /// <summary>The host of this shell's panel views.</summary>
    public PanelHost PanelHost => PanelSlot;

    private readonly MainViewModel _viewModel;

    public DesktopShellView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    private void OnPanelResizerDragged(object? sender, VectorEventArgs e) => _viewModel.ResizePanel(e.Vector.X);
}
