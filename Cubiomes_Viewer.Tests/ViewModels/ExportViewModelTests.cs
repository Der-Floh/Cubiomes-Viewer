using System.IO.Compression;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class ExportViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();
    private readonly ExportViewModel _export;
    private readonly WorldSession _session;
    private readonly MapViewModel _map;

    public ExportViewModelTests()
    {
        _session = _services.GetRequiredService<WorldSession>();
        _session.Version = McVersion.V1_21_3;
        _session.Seed = 262;
        _map = _services.GetRequiredService<MapViewModel>();
        _export = _services.GetRequiredService<ExportViewModel>();
    }

    private MemoryStorageService Storage => (MemoryStorageService)_services.GetRequiredService<IStorageService>();
    private RecordingNotificationService Notifications => (RecordingNotificationService)_services.GetRequiredService<INotificationService>();

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void Opened_TakesTheVisibleAreaAndTheScaleTheMapShows()
    {
        _map.ViewSize = (800, 600);
        _map.Camera = new MapCamera(100, -50, 4);

        _export.OnOpened();

        Assert.Equal((-1500m, -1252m, 1699m, 1151m), (_export.FromX, _export.FromZ, _export.ToX, _export.ToZ));
        Assert.Equal(4, _export.SelectedScale?.Value);
        Assert.Equal("800 × 601 pixels.", _export.SummaryText);
        Assert.Equal("Save PNG image…", _export.ExportLabel);
    }

    [AvaloniaFact]
    public async Task Export_TheSeedOnTheMap_SavesAPngOfTheAreaThatADecoderReads()
    {
        SetArea(-256, -128, 255, 127, scale: 4);

        await _export.ExportCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Equal("cubiomes-262-1.21.3-overworld-biomes.png", Storage.LastSaveOptions?.SuggestedFileName);
        using var bitmap = new Bitmap(new MemoryStream(Assert.IsType<byte[]>(Storage.SavedContent)));
        Assert.Equal(new PixelSize(128, 64), bitmap.PixelSize);
        Assert.StartsWith("Saved ", _export.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Export_FoundSeeds_SavesAnArchiveWithOneImageEach()
    {
        var search = _services.GetRequiredService<ISeedSearchService>();
        var setup = new SearchSetup { Definition = new SearchDefinition { Version = McVersion.V1_21_3, Conditions = [new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(144), Count = 14 }] }, MaxResults = 3 };
        await search.StartAsync(setup, TestContext.Current.CancellationToken);
        _export.SelectedSeeds = _export.SeedChoices.Single(choice => choice.Value == SeedScope.FoundSeeds);
        SetArea(0, 0, 255, 255, scale: 16);

        await _export.ExportCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Equal("Save ZIP archive…", _export.ExportLabel);
        using var archive = new ZipArchive(new MemoryStream(Assert.IsType<byte[]>(Storage.SavedContent)));
        Assert.Equal(search.Results.Select(seed => $"{seed}.png").Order(StringComparer.Ordinal), archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));
    }

    [AvaloniaFact]
    public void Tiles_MakeAnArchiveAndSayHowManyThereAre()
    {
        SetArea(0, 0, 999, 499, scale: 1);

        _export.UseTiles = true;
        _export.TileSize = 256;

        Assert.Equal("1,000 × 500 pixels, cut into 8 tiles, in a ZIP archive.", _export.SummaryText);
        Assert.Equal("Save ZIP archive…", _export.ExportLabel);
    }

    [AvaloniaFact]
    public void AreaTooLarge_IsAProblemThatKeepsTheExportFromStarting()
    {
        SetArea(0, 0, 99_999, 99, scale: 1);

        Assert.True(_export.HasProblems);
        Assert.Contains("cut it into tiles", Assert.Single(_export.Problems), StringComparison.Ordinal);
        Assert.False(_export.ExportCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Version_Changed_OffersTheScalesOfItsLayer()
    {
        _session.Version = McVersion.V1_12_2;

        Assert.Equal([1, 4, 16, 64, 256], _export.Scales.Select(choice => choice.Value));
        Assert.Contains(_services.GetRequiredService<IVersionCatalog>().GetName(McVersion.V1_12_2), _export.LayerText, StringComparison.Ordinal);
    }

    private void SetArea(int fromX, int fromZ, int toX, int toZ, int scale)
    {
        (_export.FromX, _export.FromZ, _export.ToX, _export.ToZ) = (fromX, fromZ, toX, toZ);
        _export.SelectedScale = _export.Scales.Single(choice => choice.Value == scale);
    }
}
