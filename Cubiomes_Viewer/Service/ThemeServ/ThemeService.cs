using Avalonia;
using Avalonia.Styling;

using Cubiomes_Viewer.Engine.Data.Settings;

namespace Cubiomes_Viewer.Service.ThemeServ;

public sealed class ThemeService : IThemeService
{
    public void Apply(AppTheme theme)
    {
        if (Application.Current is not { } application)
            return;

        application.RequestedThemeVariant = theme switch
        {
            AppTheme.System => ThemeVariant.Default,
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => throw new NotSupportedException($"Unsupported theme: {theme}"),
        };
    }
}
