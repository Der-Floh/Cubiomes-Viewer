using Avalonia;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Extensions;

namespace Cubiomes_Viewer.Service.IconStyleServ;

/// <summary>Replaces the application's dictionary of button icons with the one of the chosen style; views refer to the icons dynamically.</summary>
public sealed class IconStyleService : IIconStyleService
{
    private const string StyleKey = "IconStyle";

    public void Apply(IconStyle style) => Application.Current?.UseStyleDictionary(StyleKey, style.ToString(), GetSource(style));

    private static string GetSource(IconStyle style) => style switch
    {
        IconStyle.Outlined => "Accents/IconsOutlined.axaml",
        IconStyle.Filled => "Accents/IconsFilled.axaml",
        _ => throw new NotSupportedException($"Unsupported icon style: {style}"),
    };
}
