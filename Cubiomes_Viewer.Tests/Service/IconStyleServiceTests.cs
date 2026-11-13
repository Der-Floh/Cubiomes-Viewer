using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Service.IconStyleServ;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class IconStyleServiceTests
{
    private const string OutlinedIcons = "Accents/IconsOutlined.axaml";
    private const string FilledIcons = "Accents/IconsFilled.axaml";

    private static readonly Uri _assetBaseUri = new("avares://Cubiomes_Viewer/");

    [AvaloniaFact]
    public void Apply_FilledThenOutlined_PointsTheButtonIconsAtThatForm()
    {
        var service = new IconStyleService();
        try
        {
            service.Apply(IconStyle.Filled);
            Assert.Equal("/Assets/Icons/layers_filled.svg", GetIcon("LayersIcon"));
            Assert.Equal("/Assets/Icons/search.svg", GetIcon("SearchIcon"));
            Assert.Equal("/Assets/Icons/Markers/village.svg", GetIcon("VillageIcon"));
        }
        finally
        {
            service.Apply(IconStyle.Outlined);
        }

        Assert.Equal("/Assets/Icons/layers.svg", GetIcon("LayersIcon"));
    }

    [AvaloniaTheory]
    [InlineData("Accents/Icons.axaml")]
    [InlineData(OutlinedIcons)]
    [InlineData(FilledIcons)]
    public void Icons_OfEachDictionary_NameSvgFilesThatExist(string dictionary)
    {
        string[] paths = [.. Load(dictionary).Values.OfType<string>().Where(value => value.EndsWith(".svg", StringComparison.Ordinal))];

        Assert.NotEmpty(paths);
        Assert.All(paths, path => Assert.True(AssetLoader.Exists(new Uri(_assetBaseUri, path.TrimStart('/'))), path));
    }

    [AvaloniaFact]
    public void ButtonIcons_OfBothStyles_HaveTheSameNames()
    {
        Assert.Equal(GetKeys(OutlinedIcons), GetKeys(FilledIcons));
    }

    private static string?[] GetKeys(string dictionary) => [.. Load(dictionary).Keys.Select(key => key as string).Order(StringComparer.Ordinal)];

    private static IResourceDictionary Load(string dictionary) => new ResourceInclude(_assetBaseUri) { Source = new Uri(_assetBaseUri, dictionary) }.Loaded;

    private static string? GetIcon(string key)
        => Application.Current is { } application && application.TryGetResource(key, null, out var path) ? path as string : null;
}
