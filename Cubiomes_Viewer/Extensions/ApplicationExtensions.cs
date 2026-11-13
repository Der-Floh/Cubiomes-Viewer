using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;

namespace Cubiomes_Viewer.Extensions;

public static class ApplicationExtensions
{
    private static readonly Uri _assetBaseUri = new($"avares://{typeof(ApplicationExtensions).Assembly.GetName().Name}/");

    extension(Application application)
    {
        /// <summary>
        /// Merges the dictionary at <paramref name="source"/> in place of the one of another style. Each such dictionary
        /// names its style under <paramref name="styleKey"/>; nothing changes when the style is merged already.
        /// </summary>
        public void UseStyleDictionary(string styleKey, string style, string source)
        {
            ArgumentNullException.ThrowIfNull(application);

            var dictionaries = application.Resources.MergedDictionaries;
            var current = dictionaries.FirstOrDefault(dictionary => GetStyle(dictionary, styleKey) is not null);
            if (current is not null && string.Equals(GetStyle(current, styleKey), style, StringComparison.Ordinal))
                return;

            var replacement = new ResourceInclude(_assetBaseUri) { Source = new Uri(_assetBaseUri, source) };
            if (current is null)
                dictionaries.Add(replacement);
            else
                dictionaries[dictionaries.IndexOf(current)] = replacement;
        }
    }

    private static string? GetStyle(IResourceProvider dictionary, string styleKey)
        => dictionary.TryGetResource(styleKey, null, out var style) ? style as string : null;
}
