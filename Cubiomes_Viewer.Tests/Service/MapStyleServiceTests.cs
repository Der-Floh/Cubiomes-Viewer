using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg.Skia;

using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.MapStyleServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class MapStyleServiceTests
{
    [AvaloniaFact]
    public void GetBiomeMarker_EveryBiomeOfEveryVersion_HasAGlyphOnItsPaletteColour()
    {
        var (biomes, versions) = (new BiomeCatalog(), new VersionCatalog());
        var styles = new MapStyleService(biomes, versions);
        int[] all = [.. versions.Versions.SelectMany(version => Enum.GetValues<Dimension>().SelectMany(dimension => biomes.GetBiomes(version, dimension))).Distinct()];

        Assert.All(all, biome =>
        {
            var marker = styles.GetBiomeMarker(biome);
            var color = biomes.DefaultPalette[biome];
            Assert.True(marker.Glyph.Size.Width > 0, $"Biome {biome} has no glyph.");
            Assert.Equal(Color.FromRgb(color.Red, color.Green, color.Blue), Assert.IsAssignableFrom<ISolidColorBrush>(marker.Fill).Color);
        });
    }

    [AvaloniaFact]
    public void GetBiomeMarker_LightAndDarkBiomes_DrawTheGlyphInTheColourThatStandsOut()
    {
        const int SnowyPlains = 12;
        const int DeepOcean = 24;
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());

        Assert.Equal(GetColor("Marker.GlyphDark"), Assert.IsType<SvgImage>(styles.GetBiomeMarker(SnowyPlains).Glyph).CurrentColor);
        Assert.Equal(GetColor("Marker.Glyph"), Assert.IsType<SvgImage>(styles.GetBiomeMarker(DeepOcean).Glyph).CurrentColor);
    }

    [AvaloniaFact]
    public void GetBiomeMarker_AskedTwice_GivesOutOneBadge()
    {
        const int Forest = 4;
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());

        Assert.Same(styles.GetBiomeMarker(Forest), styles.GetBiomeMarker(Forest));
    }

    [AvaloniaFact]
    public void Markers_EveryStructureLandmarkAndDimension_HaveAGlyph()
    {
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());

        MapMarker[] markers =
        [
            .. App.Services.GetRequiredService<IStructureCatalog>().Structures.Select(structure => styles.GetStructureMarker(structure.Type)),
            .. Enum.GetValues<Dimension>().Select(styles.GetDimensionMarker),
            styles.Spawn,
            styles.Stronghold,
        ];
        Assert.All(markers, marker => Assert.True(marker.Glyph.Size.Width > 0));
    }

    [AvaloniaFact]
    public void GetPinLook_Marker_TintsItsGlyphOnceAndFallsBackToThePlainPin()
    {
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());

        var village = styles.GetPinLook(MarkerNames.Of(StructureType.Village));

        Assert.Same(village, styles.GetPinLook("Village"));
        Assert.Equal("Village", village.Icon);
        Assert.True(village.Glyph?.Size.Width > 0);
        Assert.Same(styles.PlainPin, styles.GetPinLook(null));
        Assert.Same(styles.PlainPin, styles.GetPinLook("NoSuchMarker"));
        Assert.Null(styles.PlainPin.Glyph);
    }

    [AvaloniaTheory]
    [InlineData("Home")]
    [InlineData("Search")]
    [InlineData("Delete")]
    public void GetPinLook_NameOfAnInterfaceIcon_IsThePlainPin(string icon)
    {
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());

        Assert.Same(styles.PlainPin, styles.GetPinLook(icon));
    }

    private static Color GetColor(string key)
        => Application.Current is { } application && application.TryGetResource(key, null, out var value) && value is Color color
            ? color
            : throw new InvalidOperationException($"The colour {key} is missing.");
}
