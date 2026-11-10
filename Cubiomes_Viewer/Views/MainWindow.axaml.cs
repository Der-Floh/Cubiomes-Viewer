using Avalonia.Controls;

namespace Cubiomes_Viewer.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainContainer container)
    {
        InitializeComponent();
        Content = container;
    }
}
