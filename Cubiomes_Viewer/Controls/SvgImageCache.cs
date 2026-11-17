using System.Collections.Concurrent;

using Avalonia.Media;
using Avalonia.Svg.Skia;

using Svg.Model;

using DrawingColor = System.Drawing.Color;

namespace Cubiomes_Viewer.Controls;

/// <summary>
/// Parses each SVG asset once per colour and hands every control that draws it the same image, as parsing is slow,
/// above all in the browser, and many controls draw the same icons. The colour goes into the parse: setting
/// <see cref="SvgImage.CurrentColor"/> to another colour later would parse the file again.
/// </summary>
public static class SvgImageCache
{
    private static readonly Uri _assetBaseUri = new($"avares://{typeof(SvgImageCache).Assembly.GetName().Name}/");
    private static readonly ConcurrentDictionary<(string Path, Color? Color), SvgImage> _images = new();

    /// <summary>The image of an asset such as /Assets/Icons/home.svg, with currentColor painted in <paramref name="color"/>.</summary>
    /// <remarks>The image is shared: callers must not change it.</remarks>
    public static SvgImage Get(string path, Color? color)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return _images.GetOrAdd((path, color), static key => Load(key.Path, key.Color));
    }

    private static SvgImage Load(string path, Color? color)
    {
        SvgParameters? parameters = color is { } painted ? new SvgParameters(null, null, DrawingColor.FromArgb(painted.A, painted.R, painted.G, painted.B)) : null;
        return new SvgImage { Source = SvgSource.Load(path, _assetBaseUri, parameters), CurrentColor = color };
    }
}
