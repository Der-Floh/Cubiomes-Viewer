namespace Cubiomes_Viewer.Engine.Data.Settings;

public sealed class AppearanceSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public IconStyle IconStyle { get; set; } = IconStyle.Outlined;
    public MarkerStyle MarkerStyle { get; set; } = MarkerStyle.Blocky;
}
