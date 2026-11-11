using System.ComponentModel;
using System.Globalization;

using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>The actions of the map's context menu; they refer to the block the menu was opened on.</summary>
public sealed partial class MapMenuViewModel : ViewModelBase
{
    private const string ImageExtension = "png";

    public string CopyTeleportLabel { get; } = "Copy /tp command";
    public string OpenShadowSeedLabel { get; } = "Open shadow seed";
    public string ShadowSeedHint { get; } = "The shadow seed has the same Overworld biomes in Beta 1.8 to 1.17, but other structures and terrain.";
    public string SaveImageLabel { get; } = "Save map image...";
    public string CopiedTitle { get; } = "Copied";
    public string SaveImageTitle { get; } = "Save map image";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyCoordinatesLabel), nameof(CopyChunkLabel), nameof(CopyRegionFileLabel))]
    public partial BlockPos Position { get; set; }

    public string CopyCoordinatesLabel => $"Copy coordinates ({Coordinates})";
    public string CopyChunkLabel => $"Copy chunk ({Chunk})";
    public string CopyRegionFileLabel => $"Copy region file ({RegionFile})";
    public string CopySeedLabel => $"Copy seed ({Seed})";
    public string MeasureLabel => _map.IsMeasuring ? "Stop measuring" : "Measure distance from here";

    /// <summary>The view that shows the map; set by that view while it is on screen.</summary>
    public IMapImageSource? ImageSource { get; set; }

    private string Coordinates => BlockPosText.ToCoordinates(Position);
    private string Chunk => BlockPosText.ToChunk(Position);
    private string RegionFile => BlockPosText.ToRegionFile(Position);
    private string Seed => _session.Seed.ToString(CultureInfo.InvariantCulture);
    private bool CanOpenShadowSeed => SeedMath.HasShadowBiomes(_session.Version);

    private readonly MapViewModel _map;
    private readonly WorldSession _session;
    private readonly ISeedHistoryService _seedHistory;
    private readonly IVersionCatalog _versionCatalog;
    private readonly IClipboardService _clipboardService;
    private readonly IStorageService _storageService;
    private readonly INotificationService _notificationService;

    public MapMenuViewModel(MapViewModel map, WorldSession session, ISeedHistoryService seedHistory, IVersionCatalog versionCatalog, IClipboardService clipboardService, IStorageService storageService, INotificationService notificationService) : base(notificationService)
    {
        _map = map;
        _session = session;
        _seedHistory = seedHistory;
        _versionCatalog = versionCatalog;
        _clipboardService = clipboardService;
        _storageService = storageService;
        _notificationService = notificationService;

        map.PropertyChanged += OnMapChanged;
        session.PropertyChanged += OnSessionChanged;
    }

    [RelayCommand]
    public Task CopyCoordinatesAsync() => CopyAsync(Coordinates);

    [RelayCommand]
    public Task CopyTeleportAsync() => CopyAsync(BlockPosText.ToTeleportCommand(Position));

    [RelayCommand]
    public Task CopyChunkAsync() => CopyAsync(Chunk);

    [RelayCommand]
    public Task CopyRegionFileAsync() => CopyAsync(RegionFile);

    [RelayCommand]
    public Task CopySeedAsync() => CopyAsync(Seed);

    [RelayCommand]
    public void ToggleMeasuring() => _map.MeasureStart = _map.IsMeasuring ? null : Position;

    [RelayCommand]
    public void StopMeasuring() => _map.MeasureStart = null;

    [RelayCommand(CanExecute = nameof(CanOpenShadowSeed))]
    public void OpenShadowSeed()
    {
        var shadow = SeedMath.GetShadowSeed(_session.Seed);
        _session.Seed = shadow;
        _seedHistory.Add(shadow);
    }

    [RelayCommand]
    public Task SaveImageAsync() => RunGuardedAsync(async () =>
    {
        if (ImageSource is not { } source)
            return;

        var image = source.CapturePng();
        var file = await _storageService.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = SaveImageTitle,
            SuggestedFileName = GetImageFileName(),
            DefaultExtension = ImageExtension,
            FileTypeChoices = [FilePickerFileTypes.ImagePng],
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(image);
    }, browserFireAndForget: true);

    private Task CopyAsync(string text) => RunGuardedAsync(async () =>
    {
        await _clipboardService.SetTextAsync(text);
        _notificationService.ShowSuccess(CopiedTitle, text);
    });

    private string GetImageFileName()
        => string.Create(CultureInfo.InvariantCulture, $"cubiomes-{_session.Seed}-{_versionCatalog.GetName(_session.Version)}-{_session.Dimension}.{ImageExtension}")
            .ToLowerInvariant();

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.IsMeasuring))
            OnPropertyChanged(nameof(MeasureLabel));
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorldSession.Seed))
            OnPropertyChanged(nameof(CopySeedLabel));
        if (e.PropertyName == nameof(WorldSession.Version))
            OpenShadowSeedCommand.NotifyCanExecuteChanged();
    }
}
