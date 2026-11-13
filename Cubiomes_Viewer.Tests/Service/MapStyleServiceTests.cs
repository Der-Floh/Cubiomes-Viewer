using Avalonia;
using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class MapStyleServiceTests
{
    [AvaloniaFact]
    public void MarkerStyle_Changed_SwapsTheGlyphOfMarkersGivenOutBefore()
    {
        var styles = new MapStyleService();
        var village = styles.GetStructureMarker(StructureType.Village);
        var blocky = village.Glyph;
        try
        {
            styles.MarkerStyle = MarkerStyle.Clean;

            Assert.NotSame(blocky, village.Glyph);
            Assert.Same(village, styles.GetStructureMarker(StructureType.Village));
            Assert.Equal("/Assets/Icons/Markers/Clean/village.svg", GetResource("VillageIcon"));
        }
        finally
        {
            styles.MarkerStyle = MarkerStyle.Blocky;
        }

        Assert.Equal("/Assets/Icons/Markers/Blocky/village.svg", GetResource("VillageIcon"));
    }

    [AvaloniaTheory]
    [InlineData(MarkerStyle.Blocky)]
    [InlineData(MarkerStyle.Clean)]
    public void MarkerStyle_Each_HasAGlyphForEveryStructureLandmarkAndDimension(MarkerStyle style)
    {
        var styles = new MapStyleService();
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

    private static string? GetResource(string key)
        => Application.Current is { } application && application.TryGetResource(key, null, out var value) ? value as string : null;
}
