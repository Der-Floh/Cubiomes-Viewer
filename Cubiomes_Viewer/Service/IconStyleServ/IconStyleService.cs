using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;

using Cubiomes_Viewer.Engine.Data.Settings;

namespace Cubiomes_Viewer.Service.IconStyleServ;

/// <summary>Replaces the application's dictionary of button icons with the one of the chosen style; views refer to the icons dynamically.</summary>
public sealed class IconStyleService : IIconStyleService
{
    private const string StyleKey = "IconStyle";

    private static readonly Uri _assetBaseUri = new($"avares://{typeof(IconStyleService).Assembly.GetName().Name}/");

    public void Apply(IconStyle style)
    {
        if (Application.Current is not { } application)
            return;

        var dictionaries = application.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(dictionary => GetStyle(dictionary) is not null);
        if (current is not null && string.Equals(GetStyle(current), style.ToString(), StringComparison.Ordinal))
            return;

        var replacement = new ResourceInclude(_assetBaseUri) { Source = new Uri(_assetBaseUri, GetSource(style)) };
        if (current is null)
            dictionaries.Add(replacement);
        else
            dictionaries[dictionaries.IndexOf(current)] = replacement;
    }

    private static string? GetStyle(IResourceProvider dictionary)
        => dictionary.TryGetResource(StyleKey, null, out var style) ? style as string : null;

    private static string GetSource(IconStyle style) => style switch
    {
        IconStyle.Outlined => "Accents/IconsOutlined.axaml",
        IconStyle.Filled => "Accents/IconsFilled.axaml",
        _ => throw new NotSupportedException($"Unsupported icon style: {style}"),
    };
}
