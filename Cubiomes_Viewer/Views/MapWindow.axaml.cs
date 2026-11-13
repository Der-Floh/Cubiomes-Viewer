using Avalonia.Controls;
using Avalonia.Media;

namespace Cubiomes_Viewer.Views;

/// <summary>A window of its own for the map, on the desktop; the map view is moved in and out by <see cref="MainContainer"/>.</summary>
public sealed partial class MapWindow : Window
{
    /// <summary>The panel the shared map view is placed in while it is in this window.</summary>
    public Panel MapHost => MapSlot;

    public MapWindow()
    {
        InitializeComponent();
    }

    /// <summary>Scales the window's content by the interface size, as the main window's is.</summary>
    public void ShowZoom(double scale) => Zoom.LayoutTransform = new ScaleTransform(scale, scale);
}
