using Avalonia.Controls;

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
}
