using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ExportServ;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Exports the layer the map shows as PNG images, one pixel per sample: of the seed on the map or of the found seeds,
/// over an area at a scale, whole or cut into tiles.
/// </summary>
public sealed partial class ExportViewModel : ViewModelBase, IOpenablePanel
{
    /// <summary>The most found seeds one export covers.</summary>
    public const int MaxSeeds = AnalysisScopeViewModel.MaxSeeds;

    private const int DefaultTileSize = 512;
    private const int DefaultHalfSide = 1024;
    private const string SaveImageLabel = "Save PNG image…";
    private const string SaveArchiveLabel = "Save ZIP archive…";

    private static readonly FilePickerFileType _pngFiles = new("PNG images") { Patterns = ["*.png"], MimeTypes = ["image/png"] };
    private static readonly FilePickerFileType _zipFiles = new("ZIP archives") { Patterns = ["*.zip"], MimeTypes = ["application/zip"] };

    public string Title { get; } = "Export";
    public string Hint { get; } = "Saves the layer the map shows, as the Layers panel sets it up, with one pixel per sample. Markers and the grid are left out.";
    public string SeedsLabel { get; } = "Seeds";
    public string FromLabel { get; } = "From X and Z";
    public string ToLabel { get; } = "To X and Z, included";
    public string UseVisibleAreaLabel { get; } = "Visible area";
    public string ScaleLabel { get; } = "Blocks per pixel";
    public string TilesLabel { get; } = "Cut into tiles";
    public string TileSizeLabel { get; } = "Tile side in pixels";
    public string StopLabel { get; } = "Stop";
    public string SavedTitle { get; } = "Saved";
    public decimal MinCoordinate { get; } = -TilePyramid.WorldLimitBlocks;
    public decimal MaxCoordinate { get; } = TilePyramid.WorldLimitBlocks;
    public decimal MinTileSize { get; } = MapExportService.MinTileSize;
    public decimal MaxTileSize { get; } = MapExportService.MaxTileSize;

    [ObservableProperty]
    public partial ComboChoice<SeedScope>? SelectedSeeds { get; set; }

    [ObservableProperty]
    public partial decimal? FromX { get; set; } = -DefaultHalfSide;

    [ObservableProperty]
    public partial decimal? FromZ { get; set; } = -DefaultHalfSide;

    [ObservableProperty]
    public partial decimal? ToX { get; set; } = DefaultHalfSide - 1;

    [ObservableProperty]
    public partial decimal? ToZ { get; set; } = DefaultHalfSide - 1;

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedScale { get; set; }

    [ObservableProperty]
    public partial bool UseTiles { get; set; }

    [ObservableProperty]
    public partial decimal? TileSize { get; set; } = DefaultTileSize;

    [ObservableProperty]
    public partial string LayerText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExportLabel { get; set; } = SaveImageLabel;

    [ObservableProperty]
    public partial bool HasProblems { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand), nameof(StopCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public ObservableCollection<ComboChoice<SeedScope>> SeedChoices { get; } = [];
    public ObservableCollection<ComboChoice<int>> Scales { get; } = [];

    /// <summary>What keeps the export from being made.</summary>
    public ObservableCollection<string> Problems { get; } = [];

    private readonly MapViewModel _map;
    private readonly ISeedSearchService _search;
    private readonly IMapExportService _export;
    private readonly ILayerCatalog _layerCatalog;
    private readonly IVersionCatalog _versionCatalog;
    private readonly IStorageService _storage;
    private readonly INotificationService _notifications;
    private CancellationTokenSource? _stopping;
    private int _shownFound = -1;
    private bool _isShowing;
    private bool _hasArea;

    public ExportViewModel(MapViewModel map, ISeedSearchService search, IMapExportService export, ILayerCatalog layerCatalog, IVersionCatalog versionCatalog, IStorageService storage, INotificationService notifications) : base(notifications)
    {
        _map = map;
        _search = search;
        _export = export;
        _layerCatalog = layerCatalog;
        _versionCatalog = versionCatalog;
        _storage = storage;
        _notifications = notifications;

        Show(() =>
        {
            ShowSeedChoices();
            ShowLayer();
        });
        Refresh();
        map.PropertyChanged += OnMapChanged;
        search.Changed += OnSearchChanged;
    }

    /// <summary>Takes the visible area and the map's scale the first time, so that the export starts as what is on screen.</summary>
    public void OnOpened()
    {
        if (!_hasArea)
            UseVisibleArea();
        Refresh();
    }

    /// <summary>The export as it is set up now.</summary>
    public MapExportRequest CreateRequest()
    {
        var (minX, maxX) = Order(ToBlock(FromX), ToBlock(ToX));
        var (minZ, maxZ) = Order(ToBlock(FromZ), ToBlock(ToZ));
        return new MapExportRequest
        {
            Worlds = GetWorlds(),
            Layer = _map.Layer,
            SampleHeight = _map.SampleHeight,
            Octaves = _map.Octaves,
            Style = new TileStyle(_map.HeightStyle, _map.Layer == LayerMode.StructurePotential ? _map.PotentialStructure : null, _map.BiomeColors),
            Area = new BlockArea(minX, minZ, maxX - minX + 1, maxZ - minZ + 1),
            Scale = SelectedScale?.Value ?? Scales.FirstOrDefault()?.Value ?? 1,
            TileSize = UseTiles ? (int)Math.Round(TileSize ?? DefaultTileSize) : null,
        };
    }

    [RelayCommand]
    public void UseVisibleArea()
    {
        var viewport = _map.Viewport;
        if (viewport.IsEmpty)
            return;

        Show(() =>
        {
            var shown = TilePyramid.SelectScale(TileMetrics.For(_map.World, _map.Layer), viewport.BlocksPerPixel);
            SelectedScale = Scales.FirstOrDefault(choice => choice.Value == shown) ?? SelectedScale;
            var scale = SelectedScale?.Value ?? 1;
            FromX = ToBlock((decimal)(Math.Floor(viewport.Left / scale) * scale));
            FromZ = ToBlock((decimal)(Math.Floor(viewport.Top / scale) * scale));
            ToX = ToBlock((decimal)(Math.Ceiling(viewport.Right / scale) * scale) - 1);
            ToZ = ToBlock((decimal)(Math.Ceiling(viewport.Bottom / scale) * scale) - 1);
        });
        _hasArea = true;
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportAsync() => RunGuardedAsync(async () =>
    {
        var request = CreateRequest();
        var options = new FilePickerSaveOptions
        {
            Title = ExportLabel,
            SuggestedFileName = GetFileName(request),
            DefaultExtension = request.IsArchive ? "zip" : "png",
            FileTypeChoices = [request.IsArchive ? _zipFiles : _pngFiles],
        };

        using var stopping = new CancellationTokenSource();
        _stopping = stopping;
        try
        {
            var name = await _storage.SaveFileAsync(options, stream =>
            {
                IsRunning = true;
                StatusText = "Exporting…";
                return _export.ExportAsync(request, stream, new Progress<double>(ShowProgress), stopping.Token);
            });
            if (name is not null)
            {
                StatusText = $"Saved {name}.";
                _notifications.ShowSuccess(SavedTitle, name);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            StatusText = "Stopped; nothing was saved.";
        }
        finally
        {
            _stopping = null;
            IsRunning = false;
        }
    }, browserFireAndForget: true);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _stopping?.Cancel();

    private bool CanExport() => !IsRunning && !HasProblems;

    partial void OnSelectedSeedsChanged(ComboChoice<SeedScope>? value) => Refresh();

    partial void OnFromXChanged(decimal? value) => Refresh();

    partial void OnFromZChanged(decimal? value) => Refresh();

    partial void OnToXChanged(decimal? value) => Refresh();

    partial void OnToZChanged(decimal? value) => Refresh();

    partial void OnSelectedScaleChanged(ComboChoice<int>? value) => Refresh();

    partial void OnUseTilesChanged(bool value) => Refresh();

    partial void OnTileSizeChanged(decimal? value) => Refresh();

    private void OnSearchChanged(object? sender, EventArgs e)
    {
        Show(ShowSeedChoices);
        Refresh();
    }

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapViewModel.World) or nameof(MapViewModel.Layer) or nameof(MapViewModel.SampleHeight) or nameof(MapViewModel.Octaves))
        {
            Show(ShowLayer);
            Refresh();
        }
    }

    private IReadOnlyList<WorldKey> GetWorlds()
    {
        if (SelectedSeeds?.Value != SeedScope.FoundSeeds || _search.Setup is not { } setup)
            return [_map.World];

        return [.. _search.Results.Take(MaxSeeds).Select(seed => setup.Definition.GetWorld(seed, _map.World.Dimension))];
    }

    /// <summary>Names the layer and offers the scales it can be exported at, keeping the chosen one where it can.</summary>
    private void ShowLayer()
    {
        var world = _map.World;
        var name = _layerCatalog.IsAvailable(_map.Layer, world.Version, world.Dimension) ? _layerCatalog.GetInfo(_map.Layer, world.Version, world.Dimension).Name : _map.Layer.ToString();
        var height = _map.Layer.DependsOnHeight ? Format($", at Y {_map.SampleHeight}") : string.Empty;
        LayerText = $"{name} of {_versionCatalog.GetName(world.Version)}, {world.Dimension}{height}";

        var scales = _export.GetScales(world, _map.Layer);
        if (Scales.Select(choice => choice.Value).SequenceEqual(scales))
            return;

        var wanted = SelectedScale?.Value;
        Scales.Clear();
        foreach (var scale in scales)
            Scales.Add(new ComboChoice<int> { Display = scale == 1 ? "1 (one pixel per block)" : Format($"{scale}"), Value = scale });
        SelectedScale = Scales.FirstOrDefault(choice => choice.Value == wanted) ?? Scales.MinBy(choice => Math.Abs(Math.Log(choice.Value) - Math.Log(wanted ?? 1)));
    }

    /// <summary>Offers the found seeds while there are some, keeping the choice where it can; nothing changes while their number stays.</summary>
    private void ShowSeedChoices()
    {
        var found = _search.Results.Count;
        if (found == _shownFound)
            return;

        _shownFound = found;
        var wanted = SelectedSeeds?.Value ?? SeedScope.ThisSeed;
        SeedChoices.Clear();
        SeedChoices.Add(new ComboChoice<SeedScope> { Display = "The seed on the map", Value = SeedScope.ThisSeed });
        if (found > 0)
        {
            var count = Math.Min(found, MaxSeeds).ToString("N0", CultureInfo.InvariantCulture);
            SeedChoices.Add(new ComboChoice<SeedScope> { Display = found > MaxSeeds ? $"The first {count} found seeds" : $"The found seeds ({count})", Value = SeedScope.FoundSeeds });
        }

        SelectedSeeds = SeedChoices.FirstOrDefault(choice => choice.Value == wanted) ?? SeedChoices[0];
    }

    /// <summary>Checks the export as it is set up and describes what it will make.</summary>
    private void Refresh()
    {
        if (_isShowing)
            return;

        var request = CreateRequest();
        var problems = _export.FindProblems(request);
        Problems.Clear();
        foreach (var problem in problems)
            Problems.Add(problem);

        HasProblems = Problems.Count > 0;
        SummaryText = HasProblems ? string.Empty : Describe(request);
        ExportLabel = request.IsArchive ? SaveArchiveLabel : SaveImageLabel;
        ExportCommand.NotifyCanExecuteChanged();
    }

    private static string Describe(MapExportRequest request)
    {
        var layout = request.GetLayout();
        var size = Format($"{layout.Width:N0} × {layout.Height:N0} pixels");
        var image = request.TileSize is { } tileSize ? Format($"{size}, cut into {Count(layout, tileSize):N0} tiles") : size;
        return request.Worlds.Count > 1
            ? Format($"{request.Worlds.Count:N0} images of {image}, in a ZIP archive.")
            : request.IsArchive ? $"{image}, in a ZIP archive." : $"{image}.";
    }

    private static long Count(MapImageLayout layout, int tileSize)
    {
        var (columns, rows) = layout.CountTiles(tileSize);
        return (long)columns * rows;
    }

    private string GetFileName(MapExportRequest request)
    {
        var world = request.Worlds[0];
        var seeds = request.Worlds.Count > 1 ? "found-seeds" : world.Seed.ToString(CultureInfo.InvariantCulture);
        var layer = request.Layer.ToString();
        var name = $"cubiomes-{seeds}-{_versionCatalog.GetName(world.Version)}-{world.Dimension}-{layer}.{(request.IsArchive ? "zip" : "png")}";
        return name.ToLowerInvariant();
    }

    private void ShowProgress(double share)
    {
        if (IsRunning)
            StatusText = Format($"Exporting… {share * 100:0} %");
    }

    private void Show(Action changes)
    {
        var wasShowing = _isShowing;
        _isShowing = true;
        try
        {
            changes();
        }
        finally
        {
            _isShowing = wasShowing;
        }
    }

    private static (int Low, int High) Order(int first, int second) => first <= second ? (first, second) : (second, first);

    private static int ToBlock(decimal? value) => (int)Math.Clamp(Math.Round(value ?? 0), -TilePyramid.WorldLimitBlocks, TilePyramid.WorldLimitBlocks);

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
