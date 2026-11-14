using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg.Skia;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.MapStyleServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class MapStyleServiceTests
{
    [AvaloniaFact]
    public void MarkerStyle_Changed_SwapsTheGlyphOfMarkersGivenOutBefore()
    {
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());
        var village = styles.GetStructureMarker(StructureType.Village);
        var blocky = village.Glyph;
        try
        {
            styles.MarkerStyle = MarkerStyle.Clean;

            Assert.NotSame(blocky, village.Glyph);
            Assert.Same(village, styles.GetStructureMarker(StructureType.Village));
            Assert.Equal("/Assets/Icons/Structures/Clean/village.svg", GetResource("VillageIcon"));
        }
        finally
        {
            styles.MarkerStyle = MarkerStyle.Blocky;
        }

        Assert.Equal("/Assets/Icons/Structures/Blocky/village.svg", GetResource("VillageIcon"));
    }

    [AvaloniaTheory]
    [InlineData(MarkerStyle.Blocky)]
    [InlineData(MarkerStyle.Clean)]
    public void MarkerStyle_Each_HasAGlyphForEveryBiomeOfEveryVersion(MarkerStyle style)
    {
        var (biomes, versions) = (new BiomeCatalog(), new VersionCatalog());
        var styles = new MapStyleService(biomes, versions);
        int[] all = [.. versions.Versions.SelectMany(version => Enum.GetValues<Dimension>().SelectMany(dimension => biomes.GetBiomes(version, dimension))).Distinct()];
        try
        {
            styles.MarkerStyle = style;

            Assert.All(all, biome =>
            {
                var marker = styles.GetBiomeMarker(biome);
                var color = biomes.DefaultPalette[biome];
                Assert.True(marker.Glyph.Size.Width > 0, $"Biome {biome} has no {style} glyph.");
                Assert.Equal(Color.FromRgb(color.Red, color.Green, color.Blue), Assert.IsAssignableFrom<ISolidColorBrush>(marker.Fill).Color);
            });
        }
        finally
        {
            styles.MarkerStyle = MarkerStyle.Blocky;
        }
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
    public void MarkerStyle_Changed_SwapsTheGlyphOfBiomeBadgesGivenOutBefore()
    {
        const int Forest = 4;
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());
        var forest = styles.GetBiomeMarker(Forest);
        var blocky = forest.Glyph;
        try
        {
            styles.MarkerStyle = MarkerStyle.Clean;

            Assert.NotSame(blocky, forest.Glyph);
            Assert.Same(forest, styles.GetBiomeMarker(Forest));
        }
        finally
        {
            styles.MarkerStyle = MarkerStyle.Blocky;
        }
    }

    [AvaloniaTheory]
    [InlineData(MarkerStyle.Blocky)]
    [InlineData(MarkerStyle.Clean)]
    public void MarkerStyle_Each_HasAGlyphForEveryStructureLandmarkAndDimension(MarkerStyle style)
    {
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());
        try
        {
            styles.MarkerStyle = style;

            MapMarker[] markers =
            [
                .. App.Services.GetRequiredService<IStructureCatalog>().Structures.Select(structure => styles.GetStructureMarker(structure.Type)),
                .. Enum.GetValues<Dimension>().Select(styles.GetDimensionMarker),
                styles.Spawn,
                styles.Stronghold,
            ];
            Assert.All(markers, marker => Assert.True(marker.Glyph.Size.Width > 0));
        }
        finally
        {
            styles.MarkerStyle = MarkerStyle.Blocky;
        }
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

    [AvaloniaFact]
    public void MarkerStyle_Changed_SwapsTheGlyphOfPinLooksGivenOutBefore()
    {
        var styles = new MapStyleService(new BiomeCatalog(), new VersionCatalog());
        var stronghold = styles.GetPinLook(MarkerNames.Stronghold);
        var blocky = stronghold.Glyph;
        try
        {
            styles.MarkerStyle = MarkerStyle.Clean;

            Assert.NotSame(blocky, stronghold.Glyph);
            Assert.Same(stronghold, styles.GetPinLook(MarkerNames.Stronghold));
        }
        finally
        {
            styles.MarkerStyle = MarkerStyle.Blocky;
        }
    }

    private static string? GetResource(string key)
        => Application.Current is { } application && application.TryGetResource(key, null, out var value) ? value as string : null;

    private static Color GetColor(string key)
        => Application.Current is { } application && application.TryGetResource(key, null, out var value) && value is Color color
            ? color
            : throw new InvalidOperationException($"The colour {key} is missing.");
}
