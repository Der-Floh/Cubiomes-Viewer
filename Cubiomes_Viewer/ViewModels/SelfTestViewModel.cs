using System.Collections.ObjectModel;

using Avalonia.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.TileBitmapServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Runs the generator checks and a tile benchmark on the compute workers, the same way the map generates its tiles.</summary>
public sealed partial class SelfTestViewModel : ViewModelBase
{
    private const long SampleSeed = 262;
    private const int SampleScale = 16;
    private const int SampleSize = 128;
    private const int SampleHeight = 64;
    private static readonly JobPriority _priority = new(JobPriorityClass.Interactive);

    public string TitleLabel { get; } = "Native generator self-test";
    public string RunLabel { get; } = "Run self-test";
    public string SampleTileLabel { get; } = "Sample tile (seed 262, 1:16)";
    public string RunningLabel { get; } = "Running checks...";

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BenchmarkText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFailures { get; set; }

    [ObservableProperty]
    public partial Bitmap? SampleTile { get; set; }

    public ObservableCollection<SelfTestCheck> Checks { get; } = [];

    private readonly IComputeService _computeService;
    private readonly ITileBenchmarkService _benchmarkService;
    private readonly IVersionCatalog _versionCatalog;
    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ITileColorizer _tileColorizer;
    private readonly ITileBitmapService _tileBitmapService;

    public SelfTestViewModel(IComputeService computeService, ITileBenchmarkService benchmarkService, IVersionCatalog versionCatalog, IBiomeCatalog biomeCatalog, ITileColorizer tileColorizer, ITileBitmapService tileBitmapService, INotificationService notificationService) : base(notificationService)
    {
        _computeService = computeService;
        _benchmarkService = benchmarkService;
        _versionCatalog = versionCatalog;
        _biomeCatalog = biomeCatalog;
        _tileColorizer = tileColorizer;
        _tileBitmapService = tileBitmapService;
    }

    partial void OnSampleTileChanged(Bitmap? oldValue, Bitmap? newValue) => oldValue?.Dispose();

    [RelayCommand]
    public Task Run() => RunGuardedAsync(async () =>
    {
        IsRunning = true;
        try
        {
            ShowReport(await _computeService.RunAsync(new SelfTestJob(), _priority));
            ShowSampleTile(await _computeService.RunAsync(CreateSampleTileJob(), _priority));
            BenchmarkText = (await _benchmarkService.RunAsync()).ToString();
        }
        finally
        {
            IsRunning = false;
        }
    });

    private void ShowReport(SelfTestReport report)
    {
        Checks.Clear();
        foreach (var check in report.Checks)
            Checks.Add(check);

        Summary = report.ToString();
        HasFailures = !report.Passed;
    }

    private void ShowSampleTile(TileData tile)
    {
        var pixels = new byte[tile.Biomes.Length * BiomeTileColorizer.BytesPerPixel];
        _tileColorizer.Colorize(tile.Biomes, _biomeCatalog.DefaultPalette, pixels);
        SampleTile = _tileBitmapService.Create(pixels, tile.Samples, tile.Samples);
    }

    private BiomeTileJob CreateSampleTileJob()
    {
        var world = new WorldKey(SampleSeed, _versionCatalog.Newest, Dimension.Overworld, GeneratorFlags.None);
        return new BiomeTileJob(new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(SampleScale, 0, 0)), SampleSize);
    }
}
