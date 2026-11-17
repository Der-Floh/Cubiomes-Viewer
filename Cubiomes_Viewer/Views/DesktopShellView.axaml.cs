using Avalonia.Controls;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

public sealed partial class DesktopShellView : UserControl
{
    /// <summary>The panel the shared map view is placed in while this shell is active.</summary>
    public Panel MapHost => MapSlot;

    /// <summary>The host of this shell's panel views.</summary>
    public PanelHost PanelHost => PanelSlot;

    public DesktopShellView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
