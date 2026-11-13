namespace Cubiomes_Viewer.Engine.Data.Settings;

public sealed class AppearanceSettings
{
    public const int DefaultUiZoom = 100;

    public AppTheme Theme { get; set; } = AppTheme.System;
    public IconStyle IconStyle { get; set; } = IconStyle.Outlined;
    public MarkerStyle MarkerStyle { get; set; } = MarkerStyle.Blocky;

    /// <summary>How large the interface is drawn, in percent of its normal size.</summary>
    public int UiZoom { get; set; } = DefaultUiZoom;
}
