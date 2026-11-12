using System.Text;

using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.ColorMapRepo;
using Cubiomes_Viewer.Engine.Service.ColorMapServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.ColorMapServ;

public sealed class ColorMapServiceTests : IDisposable
{
    private const int Plains = 1;
    private const int Desert = 2;

    private static readonly RgbColor _pink = new(0xff, 0x66, 0xcc);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAppDataStore _store;
    private readonly ColorMapRepository _repository;
    private readonly ColorMapService _service;

    public ColorMapServiceTests()
    {
        _store = new FileSystemAppDataStore(_directory);
        _repository = new ColorMapRepository(_store, NullLogger<ColorMapRepository>.Instance);
        _service = new ColorMapService(_repository, new BiomeCatalog());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Theory]
    [InlineData("#8db360", 0x8d, 0xb3, 0x60)]
    [InlineData("8DB360", 0x8d, 0xb3, 0x60)]
    [InlineData("  #000000 ", 0, 0, 0)]
    public void Hex_IsRead(string text, byte red, byte green, byte blue)
    {
        Assert.True(RgbColor.TryParseHex(text, out var color));
        Assert.Equal(new RgbColor(red, green, blue), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#8db36")]
    [InlineData("#8db3600")]
    [InlineData("#gggggg")]
    [InlineData(null)]
    public void Hex_ThatIsNoColour_IsRefused(string? text) => Assert.False(RgbColor.TryParseHex(text, out _));

    [Fact]
    public void Hex_WrittenAndRead_StaysTheSame() => Assert.Equal("#ff66cc", _pink.ToHex());

    [Fact]
    public async Task SavedMaps_AreLoadedAgainInOrderOfTheirNames()
    {
        await _service.SaveAsync(Map("Zebra", Plains, _pink), cancellationToken: TestContext.Current.CancellationToken);
        await _service.SaveAsync(Map("alpine", Desert, _pink), cancellationToken: TestContext.Current.CancellationToken);
        var reloaded = new ColorMapService(_repository, new BiomeCatalog());

        await reloaded.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["alpine", "Zebra"], reloaded.Maps.Select(map => map.Name));
        Assert.Equal(_service.Maps, reloaded.Maps);
    }

    [Fact]
    public async Task SavedUnderANewName_ReplacesTheMapItWas()
    {
        await _service.SaveAsync(Map("Old", Plains, _pink), cancellationToken: TestContext.Current.CancellationToken);

        await _service.SaveAsync(Map("New", Plains, _pink), previousName: "Old", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["New"], _service.Maps.Select(map => map.Name));
    }

    [Fact]
    public async Task Import_OfATakenName_StoresTheMapUnderTheNextFreeOne()
    {
        await _service.SaveAsync(Map("Pastel", Plains, _pink), cancellationToken: TestContext.Current.CancellationToken);
        var shared = _service.Export(Map("Pastel", Desert, _pink));

        var imported = await _service.ImportAsync(shared, TestContext.Current.CancellationToken);

        Assert.Equal("Pastel 2", Assert.Single(imported).Name);
        Assert.Equal(["Pastel", "Pastel 2"], _service.Maps.Select(map => map.Name));
    }

    [Fact]
    public void Export_WritesColoursAsTheyAreWrittenByHand()
    {
        var json = Encoding.UTF8.GetString(_service.Export(Map("Pastel", Plains, _pink)));

        Assert.Contains("\"color\": \"#ff66cc\"", json, StringComparison.Ordinal);
        Assert.Contains("\"biome\": 1", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"maps\":[{\"colors\":[]}]}")]
    [InlineData("{\"maps\":[{\"name\":\"X\",\"colors\":[{\"biome\":255,\"color\":\"#000000\"}]}]}")]
    [InlineData("{\"maps\":[{\"name\":\"X\",\"colors\":[{\"biome\":1,\"color\":\"green\"}]}]}")]
    [InlineData("{\"schemaVersion\":2,\"maps\":[]}")]
    [InlineData("null")]
    public async Task Import_OfSomethingElse_IsRefusedAndStoresNothing(string content)
    {
        await Assert.ThrowsAsync<FormatException>(() => _service.ImportAsync(Encoding.UTF8.GetBytes(content), TestContext.Current.CancellationToken));

        Assert.Empty(_service.Maps);
    }

    [Fact]
    public async Task Load_OfAnUnreadableStore_StartsWithoutMaps()
    {
        await _store.WriteAsync(ColorMapRepository.StorageKey, Encoding.UTF8.GetBytes("{ broken"), TestContext.Current.CancellationToken);

        await _service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_service.Maps);
    }

    [Fact]
    public void Palette_OfAMap_ChangesOnlyItsBiomes()
    {
        var palette = _service.GetPalette(Map("Pastel", Plains, _pink));

        Assert.Equal(_pink, palette[Plains]);
        Assert.Equal(_service.DefaultPalette[Desert], palette[Desert]);
        Assert.Same(_service.DefaultPalette, _service.GetPalette(null));
    }

    [Fact]
    public void Colorizer_WithAPalette_PaintsTheBiomesInIt()
    {
        var colorizer = TestEngine.CreateTileColorizer();
        var world = new WorldKey(1, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
        var tile = new TileData { Request = new TileRequest(world, LayerMode.Biomes, 0, new TileKey(4, 0, 0)), Samples = 1, Biomes = [Plains] };
        var bgra = new byte[4];

        colorizer.Colorize(tile, new TileStyle(Colors: _service.GetPalette(Map("Pastel", Plains, _pink))), bgra);

        Assert.Equal([_pink.Blue, _pink.Green, _pink.Red, 255], bgra);
    }

    [Fact]
    public void Colorizer_WithAPaletteAndAStructure_DimsThePalettesColours()
    {
        var colorizer = TestEngine.CreateTileColorizer();
        var palette = _service.GetPalette(Map("Pastel", Desert, _pink));

        var colors = colorizer.GetBiomeColors(new TileStyle(PotentialOf: StructureType.Village, Colors: palette), McVersion.V1_21_3);

        Assert.Equal(_pink, colors[Desert]);
        Assert.Equal(palette[0].ToDimmed(), colors[0]);
    }

    private static BiomeColorMap Map(string name, int biome, RgbColor color) => new() { Name = name, Colors = [new BiomeColor(biome, color)] };
}
