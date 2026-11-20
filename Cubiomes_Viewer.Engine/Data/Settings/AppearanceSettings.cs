namespace Cubiomes_Viewer.Engine.Data.Settings;

public sealed class AppearanceSettings
{
    public const int DefaultUiZoom = 100;
    public const double DefaultPanelWidth = 320;

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>How large the interface is drawn, in percent of its normal size.</summary>
    public int UiZoom { get; set; } = DefaultUiZoom;

    /// <summary>How wide the side panel is on the desktop, in pixels of the normal interface size.</summary>
    public double PanelWidth { get; set; } = DefaultPanelWidth;
}
