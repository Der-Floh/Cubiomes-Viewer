using Avalonia.Controls;

using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

public sealed partial class MobileShellView : UserControl
{
    /// <summary>The panel the shared map view is placed in while this shell is active.</summary>
    public Panel MapHost => MapSlot;

    public MobileShellView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
