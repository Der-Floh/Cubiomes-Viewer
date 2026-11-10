using System.Collections.ObjectModel;

using Avalonia.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.TileBitmapServ;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class SelfTestViewModel : ViewModelBase
{
    private const long SampleSeed = 262;
    private const int SampleScale = 16;
    private const int SampleSize = 128;
    private const int SampleHeight = 16;

    public string TitleLabel { get; } = "Native generator self-test";
    public string RunLabel { get; } = "Run self-test";
    public string SampleTileLabel { get; } = "Sample tile (seed 262, 1:16)";
    public string RunningLabel { get; } = "Running checks...";

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFailures { get; set; }

    [ObservableProperty]
    public partial Bitmap? SampleTile { get; set; }

    public ObservableCollection<SelfTestCheck> Checks { get; } = [];

    private readonly ISelfTestService _selfTestService;
    private readonly IWorldGeneratorFactory _generatorFactory;
    private readonly IVersionCatalog _versionCatalog;
    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ITileColorizer _tileColorizer;
    private readonly ITileBitmapService _tileBitmapService;

    public SelfTestViewModel(ISelfTestService selfTestService, IWorldGeneratorFactory generatorFactory, IVersionCatalog versionCatalog, IBiomeCatalog biomeCatalog, ITileColorizer tileColorizer, ITileBitmapService tileBitmapService, INotificationService notificationService) : base(notificationService)
    {
        _selfTestService = selfTestService;
        _generatorFactory = generatorFactory;
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
            var report = await RunInBackgroundAsync(_selfTestService.Run);
            Checks.Clear();
            foreach (var check in report.Checks)
                Checks.Add(check);
            Summary = report.ToString();
            HasFailures = !report.Passed;

            var pixels = await RunInBackgroundAsync(RenderSampleTile);
            SampleTile = _tileBitmapService.Create(pixels, SampleSize, SampleSize);
        }
        finally
        {
            IsRunning = false;
        }
    });

    private byte[] RenderSampleTile()
    {
        using var generator = _generatorFactory.Create(_versionCatalog.Newest, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, SampleSeed);

        var area = new BiomeArea(SampleScale, -SampleSize / 2, -SampleSize / 2, SampleSize, SampleSize, SampleHeight);
        var biomes = new byte[area.SampleCount];
        generator.GenerateBiomes(area, biomes);

        var pixels = new byte[biomes.Length * BiomeTileColorizer.BytesPerPixel];
        _tileColorizer.Colorize(biomes, _biomeCatalog.DefaultPalette, pixels);
        return pixels;
    }

    private static async Task<T> RunInBackgroundAsync<T>(Func<T> work)
    {
        if (!OperatingSystem.IsBrowser())
            return await Task.Run(work);

        // Single-threaded WASM has no thread pool; yield once so the busy state renders first.
        await Task.Yield();
        return work();
    }
}
