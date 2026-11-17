using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg.Skia;

using Cubiomes_Viewer.Controls;

using Svg.Model;

using DrawingColor = System.Drawing.Color;

namespace Cubiomes_Viewer.Tests.Controls;

public sealed class SvgImageCacheTests
{
    private const string IconPath = "/Assets/Icons/home.svg";

    private static readonly Uri _assetBaseUri = new("avares://Cubiomes_Viewer/");

    [AvaloniaFact]
    public void Get_SameFileAndColour_ReturnsTheSameImage()
    {
        var first = SvgImageCache.Get(IconPath, Colors.Red);
        var second = SvgImageCache.Get(IconPath, Colors.Red);

        Assert.Same(first, second);
    }

    [AvaloniaFact]
    public void Get_AnotherColour_ParsesTheFileInThatColour()
    {
        var red = SvgImageCache.Get(IconPath, Colors.Red);
        var blue = SvgImageCache.Get(IconPath, Colors.Blue);

        Assert.NotSame(red, blue);
        Assert.Equal(Colors.Red, red.CurrentColor);
        Assert.Equal(Colors.Blue, blue.CurrentColor);
        Assert.NotSame(red.Source?.Picture, blue.Source?.Picture);
    }

    /// <summary>The cache parses a file in its colour and then gives the image that same colour, which must not parse it again.</summary>
    [AvaloniaFact]
    public void CurrentColor_SetToTheColourOfTheParse_KeepsThePicture()
    {
        var source = SvgSource.Load(IconPath, _assetBaseUri, new SvgParameters(null, null, DrawingColor.FromArgb(255, 0, 128, 0)));
        var parsed = source.Picture;

        var image = new SvgImage { Source = source, CurrentColor = Color.FromRgb(0, 128, 0) };

        Assert.NotNull(parsed);
        Assert.Same(parsed, image.Source?.Picture);
    }
}
