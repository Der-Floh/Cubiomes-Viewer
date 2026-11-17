using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.AppAddressServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.MapLinkServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>The actions of the map's context menu; they refer to the block the menu was opened on.</summary>
public sealed partial class MapMenuViewModel : ViewModelBase
{
    private const string ImageExtension = "png";

    public string CopyTeleportLabel { get; } = "Copy /tp command";
    public string CopyLinkLabel { get; } = "Copy link to this spot";
    public string OpenShadowSeedLabel { get; } = "Open shadow seed";
    public string ShadowSeedHint { get; } = "The shadow seed has the same Overworld biome layout in Beta 1.8 to 1.17. Its structures and terrain differ, and since 1.13 its oceans have other temperatures.";
    public string SaveImageLabel { get; } = "Save map image...";
    public string PlacePinLabel { get; } = "Place a pin here";
    public string CopiedTitle { get; } = "Copied";
    public string SaveImageTitle { get; } = "Save map image";
    public string SavedTitle { get; } = "Saved";
    public string FromOverworldTitle { get; } = "From the Overworld";
    public string FromNetherTitle { get; } = "From the Nether";
    public string FindStructureLabel { get; } = "Find nearest structure";
    public string FindBiomeLabel { get; } = "Find nearest biome";

    /// <summary>The structures Find nearest offers in the open world, replaced only when the version or dimension offers others.</summary>
    public ObservableCollection<NearestTargetViewModel> StructureTargets { get; } = [];

    /// <summary>The biomes Find nearest offers in the open world, replaced only when the version or dimension offers others.</summary>
    public ObservableCollection<NearestTargetViewModel> BiomeTargets { get; } = [];

    [ObservableProperty]
    public partial bool HasStructureTargets { get; set; }

    [ObservableProperty]
    public partial bool HasBiomeTargets { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyCoordinatesLabel), nameof(CopyChunkLabel), nameof(CopyRegionFileLabel), nameof(OtherDimensionLabel))]
    public partial BlockPos Position { get; set; }

    public string CopyCoordinatesLabel => $"Copy coordinates ({Coordinates})";
    public string CopyChunkLabel => $"Copy chunk ({Chunk})";
    public string CopyRegionFileLabel => $"Copy region file ({RegionFile})";
    public string CopySeedLabel => $"Copy seed ({Seed})";
    public string MeasureLabel => _map.IsMeasuring ? "Stop measuring" : "Measure distance from here";

    public string OtherDimensionLabel => IsInNether
        ? $"Open in the Overworld ({BlockPosText.ToCoordinates(NetherMath.ToOverworld(Position))})"
        : $"Open in the Nether ({BlockPosText.ToCoordinates(NetherMath.ToNether(Position))})";

    /// <summary>Whether the open dimension has a linked one: the Nether for the Overworld and the other way round, none for the End.</summary>
    public bool CanOpenOtherDimension => _session.Dimension != Dimension.End;

    /// <summary>The view that shows the map; set by that view while it is on screen.</summary>
    public IMapImageSource? ImageSource { get; set; }

    private string Coordinates => BlockPosText.ToCoordinates(Position);
    private string Chunk => BlockPosText.ToChunk(Position);
    private string RegionFile => BlockPosText.ToRegionFile(Position);
    private string Seed => _session.Seed.ToString(CultureInfo.InvariantCulture);
    private bool CanOpenShadowSeed => SeedMath.HasShadowBiomes(_session.Version);
    private bool IsInNether => _session.Dimension == Dimension.Nether;

    private readonly MapViewModel _map;
    private readonly NearestSearchViewModel _nearest;
    private readonly WorldSession _session;
    private readonly ISeedHistoryService _seedHistory;
    private readonly IVersionCatalog _versionCatalog;
    private readonly IMapLinkService _mapLinks;
    private readonly IAppAddressService _address;
    private readonly IClipboardService _clipboardService;
    private readonly IStorageService _storageService;
    private readonly INotificationService _notificationService;
    private readonly IMapStyleService _styleService;

    public MapMenuViewModel(MapViewModel map, NearestSearchViewModel nearest, WorldSession session, ISeedHistoryService seedHistory, IVersionCatalog versionCatalog, IMapLinkService mapLinks,
        IAppAddressService address, IClipboardService clipboardService, IStorageService storageService, INotificationService notificationService,
        IMapStyleService styleService) : base(notificationService)
    {
        _styleService = styleService;
        _map = map;
        _nearest = nearest;
        _session = session;
        _seedHistory = seedHistory;
        _versionCatalog = versionCatalog;
        _mapLinks = mapLinks;
        _address = address;
        _clipboardService = clipboardService;
        _storageService = storageService;
        _notificationService = notificationService;

        ShowTargets();
        map.PropertyChanged += OnMapChanged;
        session.PropertyChanged += OnSessionChanged;
    }

    [RelayCommand]
    public Task CopyCoordinatesAsync() => CopyAsync(Coordinates);

    [RelayCommand]
    public void PlacePin() => _map.PlacePin(Position);

    [RelayCommand]
    public Task CopyTeleportAsync() => CopyAsync(BlockPosText.ToTeleportCommand(Position));

    [RelayCommand]
    public Task CopyChunkAsync() => CopyAsync(Chunk);

    [RelayCommand]
    public Task CopyRegionFileAsync() => CopyAsync(RegionFile);

    [RelayCommand]
    public Task CopySeedAsync() => CopyAsync(Seed);

    /// <summary>Copies a link to the open view centred on the menu's block, at the zoom the map has, with the pins of the map.</summary>
    [RelayCommand]
    public Task CopyLinkAsync() => CopyAsync(_mapLinks.Format(_map.GetLink() with { Center = Position }, _address.LinkBase));

    [RelayCommand]
    public void ToggleMeasuring() => _map.MeasureStart = _map.IsMeasuring ? null : Position;

    [RelayCommand]
    public void StopMeasuring() => _map.MeasureStart = null;

    /// <summary>Opens the linked dimension at the block that lines up with the menu's block, and marks it with the pin of the dimension it came from.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenOtherDimension))]
    public void OpenOtherDimension()
    {
        var (from, origin) = (_session.Dimension, Position);
        var (dimension, target, title) = IsInNether
            ? (Dimension.Overworld, NetherMath.ToOverworld(origin), FromNetherTitle)
            : (Dimension.Nether, NetherMath.ToNether(origin), FromOverworldTitle);

        _session.Dimension = dimension;
        _map.JumpTo(target);
        _map.Selection = MapSelection.ForSpot(title, target, $"{from} {BlockPosText.ToBlock(origin)}", _styleService.GetDimensionMarker(from),
            _styleService.GetPinLook(MarkerNames.Of(from)));
    }

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
        var fileName = await _storageService.SaveFileAsync(new FilePickerSaveOptions
        {
            Title = SaveImageTitle,
            SuggestedFileName = GetImageFileName(),
            DefaultExtension = ImageExtension,
            FileTypeChoices = [FilePickerFileTypes.ImagePng],
        }, image);
        if (fileName is not null)
            _notificationService.ShowSuccess(SavedTitle, fileName);
    }, browserFireAndForget: true);

    private Task CopyAsync(string text) => RunGuardedAsync(async () =>
    {
        await _clipboardService.SetTextAsync(text);
        _notificationService.ShowSuccess(CopiedTitle, text);
    });

    private Task FindNearestAsync(NearestTarget target) => _nearest.FindAsync(target, Position);

    private void ShowTargets()
    {
        Replace(StructureTargets, _nearest.GetStructureTargets(_session.Version, _session.Dimension));
        Replace(BiomeTargets, _nearest.GetBiomeTargets(_session.Version, _session.Dimension));
        HasStructureTargets = StructureTargets.Count > 0;
        HasBiomeTargets = BiomeTargets.Count > 0;
    }

    /// <summary>Lists other targets only when they differ, as a menu whose items are rebuilt while one is clicked stays open.</summary>
    private void Replace(ObservableCollection<NearestTargetViewModel> items, IReadOnlyList<NearestTarget> targets)
    {
        if (items.Select(item => item.Target).SequenceEqual(targets))
            return;

        items.Clear();
        foreach (var target in targets)
            items.Add(new NearestTargetViewModel(target, _nearest.GetMarker, FindNearestAsync));
    }

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
        if (e.PropertyName == nameof(WorldSession.Dimension))
        {
            OnPropertyChanged(nameof(OtherDimensionLabel));
            OnPropertyChanged(nameof(CanOpenOtherDimension));
            OpenOtherDimensionCommand.NotifyCanExecuteChanged();
        }
        if (e.PropertyName is nameof(WorldSession.Version) or nameof(WorldSession.Dimension))
            ShowTargets();
    }
}
