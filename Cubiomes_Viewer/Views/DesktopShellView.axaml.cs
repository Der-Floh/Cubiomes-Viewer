using Avalonia.Controls;

using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

public sealed partial class DesktopShellView : UserControl
{
    /// <summary>The panel the shared map view is placed in while this shell is active.</summary>
    public Panel MapHost => MapSlot;

    public DesktopShellView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
