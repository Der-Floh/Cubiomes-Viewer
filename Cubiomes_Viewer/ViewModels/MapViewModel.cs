using System.Collections.Frozen;
using System.ComponentModel;
using System.Globalization;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.MeasurementServ;
using Cubiomes_Viewer.Engine.Service.PinServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.ViewHistoryServ;

using EngineConstants = Cubiomes_Viewer.Engine.Data.Constants;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class MapViewModel : ViewModelBase
{
    private const double ButtonZoomStep = 2;
    private const double BlockMiddle = 0.5;

    public string GeneratingLabel { get; } = "Generating...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleText), nameof(Viewport))]
    public partial MapCamera Camera { get; set; } = MapCamera.Default;

    [ObservableProperty]
    public partial WorldKey World { get; set; }

    [ObservableProperty]
    public partial int SampleHeight { get; set; }

    /// <summary>The layer the map shows: the one the session asks for, or biomes where the world does not have it.</summary>
    [ObservableProperty]
    public partial LayerMode Layer { get; set; }

    [ObservableProperty]
    public partial int Octaves { get; set; }

    [ObservableProperty]
    public partial HeightStyle HeightStyle { get; set; } = HeightStyle.Shaded;

    /// <summary>Whether the biome map is shaded by the heights of the terrain where the world has them.</summary>
    [ObservableProperty]
    public partial bool BiomeRelief { get; set; }

    /// <summary>Whether the callout of a picked structure lists what its chests hold.</summary>
    [ObservableProperty]
    public partial bool ShowChestLoot { get; set; }

    /// <summary>The colours biomes are painted in; the default ones when it is <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial BiomePalette? BiomeColors { get; set; }

    /// <summary>The structure type whose biomes the structure potential layer highlights.</summary>
    [ObservableProperty]
    public partial StructureType PotentialStructure { get; set; } = StructureType.Village;

    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    [ObservableProperty]
    public partial int GridSpacing { get; set; } = MapSettings.DefaultGridSpacing;

    [ObservableProperty]
    public partial int GridMultiplier { get; set; } = MapSettings.DefaultGridMultiplier;

    /// <summary>How much larger or smaller than normal markers are drawn.</summary>
    [ObservableProperty]
    public partial double MarkerScale { get; set; } = 1;

    /// <summary>How far out structure types are marked where it differs from their defaults, in blocks per pixel.</summary>
    [ObservableProperty]
    public partial IReadOnlyDictionary<StructureType, double> MarkerZoomLimits { get; set; } = FrozenDictionary<StructureType, double>.Empty;

    [ObservableProperty]
    public partial bool ShowSlimeChunks { get; set; }

    [ObservableProperty]
    public partial bool ShowSpawn { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowStrongholds { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowGatewayLinks { get; set; } = true;

    /// <summary>The marker that is picked on the map, or <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial MapSelection? Selection { get; set; }

    /// <summary>The structure types marked on the map.</summary>
    [ObservableProperty]
    public partial IReadOnlySet<StructureType> VisibleStructures { get; set; } = FrozenSet<StructureType>.Empty;

    /// <summary>Per structure type, the bits of the feature options whose structures are not marked; types without hidden options are left out.</summary>
    [ObservableProperty]
    public partial IReadOnlyDictionary<StructureType, ulong> HiddenStructureOptions { get; set; } = FrozenDictionary<StructureType, ulong>.Empty;

    [ObservableProperty]
    public partial bool SmoothMotion { get; set; } = true;

    [ObservableProperty]
    public partial string HoverText { get; set; } = string.Empty;

    /// <summary>The block under the pointer, or <see langword="null"/> while the pointer is outside the map.</summary>
    [ObservableProperty]
    public partial BlockPos? HoverPosition { get; set; }

    /// <summary>The block a distance is measured from, or <see langword="null"/> when nothing is measured.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMeasuring))]
    public partial BlockPos? MeasureStart { get; set; }

    public bool IsMeasuring => MeasureStart is not null;

    /// <summary>Whether the next click on the map puts a pin down.</summary>
    [ObservableProperty]
    public partial bool IsPlacingPin { get; set; }

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    /// <summary>The size of the map on screen in pixels; set by the view that shows it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Viewport))]
    public partial (double Width, double Height) ViewSize { get; set; }

    /// <summary>What the map shows right now: its camera on a view of <see cref="ViewSize"/>.</summary>
    public MapViewport Viewport => new(Camera, ViewSize.Width, ViewSize.Height);

    public string ScaleText => Camera.BlocksPerPixel >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"1 px = {Camera.BlocksPerPixel:0.##} blocks")
        : string.Create(CultureInfo.InvariantCulture, $"1 block = {1 / Camera.BlocksPerPixel:0.##} px");

    /// <summary>Whether Back has a view the map jumped away from to return to.</summary>
    public bool CanGoBack => _history.CanGoBack;

    /// <summary>Whether Forward has a view that Back left to return to.</summary>
    public bool CanGoForward => _history.CanGoForward;

    /// <summary>Raised when the map should travel to a camera; the view glides there or jumps, as the navigation asks.</summary>
    public event Action<MapNavigation>? NavigationRequested;

    private readonly WorldSession _session;
    private readonly IMapTileService _tileService;
    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ILayerCatalog _layerCatalog;
    private readonly IPinService _pinService;
    private readonly IMeasurementService _measurementService;
    private readonly IViewHistoryService _history;
    private int _stateUpdateQueued;
    private bool _isHistoryStarted;
    private bool _isJumpUnderWay;
    private bool _isMovingInHistory;

    private MapContent Content => MapContent.For(World, Layer, SampleHeight, Octaves);

    public MapViewModel(WorldSession session, IMapTileService tileService, IBiomeCatalog biomeCatalog, ILayerCatalog layerCatalog, IPinService pinService,
        IMeasurementService measurementService, IViewHistoryService history, INotificationService notificationService) : base(notificationService)
    {
        _session = session;
        _tileService = tileService;
        _biomeCatalog = biomeCatalog;
        _layerCatalog = layerCatalog;
        _pinService = pinService;
        _measurementService = measurementService;
        _history = history;

        ShowSession();
        session.PropertyChanged += OnSessionChanged;
        tileService.StateChanged += OnTileStateChanged;
        history.Changed += OnHistoryChanged;
        history.Moved += OpenStep;
    }

    /// <summary>Starts keeping the views the map jumps between, with the view shown now as the first step.</summary>
    public void StartHistory()
    {
        _isHistoryStarted = true;
        _history.Show(GetShownView());
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public void GoBack() => _history.GoBack();

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    public void GoForward() => _history.GoForward();

    /// <summary>Steps back for the system's Back; false when there is no view to return to.</summary>
    public bool TryGoBack()
    {
        if (!CanGoBack)
            return false;

        GoBack();
        return true;
    }

    [RelayCommand]
    public void GoToOrigin() => GoTo(0, 0);

    [RelayCommand]
    public void ZoomIn() => Navigate(Camera with { BlocksPerPixel = Camera.BlocksPerPixel / ButtonZoomStep }, animate: true);

    [RelayCommand]
    public void ZoomOut() => Navigate(Camera with { BlocksPerPixel = Camera.BlocksPerPixel * ButtonZoomStep }, animate: true);

    public void GoTo(double blockX, double blockZ)
    {
        RecordJump();
        Navigate(Camera with { CenterX = blockX, CenterZ = blockZ }, animate: true);
    }

    /// <summary>Shows a camera straight away, without gliding there.</summary>
    public void JumpTo(MapCamera camera)
    {
        RecordJump();
        Navigate(camera, animate: false);
    }

    /// <summary>Centres the map on the middle of a block straight away, keeping the zoom.</summary>
    public void JumpTo(BlockPos block) => JumpTo(CenterOn(block));

    /// <summary>Glides to the middle of a block, zooming in to <paramref name="maxBlocksPerPixel"/> if the map is zoomed out further.</summary>
    public void FlyTo(BlockPos block, double maxBlocksPerPixel = double.PositiveInfinity)
    {
        RecordJump();
        Navigate(CenterOn(block) with { BlocksPerPixel = Math.Min(Camera.BlocksPerPixel, maxBlocksPerPixel) }, animate: true);
    }

    /// <summary>
    /// Opens the view a link carries: its world and layer, then its place straight away, and adds its pins to those of the map, except
    /// where the map has a pin on the same block already. The version and dimension come before the layer, because a world without the
    /// layer falls back to biomes.
    /// </summary>
    public void Open(MapLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        RecordJump();
        _session.Version = link.Version;
        _session.LargeBiomes = link.LargeBiomes;
        _session.Dimension = link.Dimension;
        _session.Layer = link.Layer;
        _session.Octaves = link.Octaves;
        _session.SampleHeight = link.SampleHeight;
        _session.Seed = link.Seed;
        _pinService.AddCopies(link.Pins.Select(pin => pin.ToPin(link.Seed, link.Dimension)));
        JumpTo(link.Camera);
    }

    /// <summary>A link to the open view, with the pins of its map.</summary>
    public MapLink GetLink()
        => MapLink.Of(_session, Camera) with { Pins = [.. _pinService.GetPins(_session.Seed, _session.Dimension).Select(LinkPin.Of)] };

    /// <summary>Puts a pin on a block of the open map, stops placing pins and picks the new pin, so its look can be chosen.</summary>
    public void PlacePin(BlockPos block)
    {
        var pin = _pinService.Add(World.Seed, World.Dimension, block);
        IsPlacingPin = false;
        Selection = MapSelection.ForPin(pin);
    }

    partial void OnMeasureStartChanged(BlockPos? value) => SetHover(HoverPosition);

    partial void OnCameraChanged(MapCamera value) => ShowInHistory();

    /// <summary>Keeps the line being measured, from its start to a block, and picks it so it can be named; ending on the start only stops measuring.</summary>
    public void FinishMeasuring(BlockPos end)
    {
        if (MeasureStart is not { } start)
            return;

        MeasureStart = null;
        if (end != start)
            Selection = MapSelection.ForMeasurement(_measurementService.Add(World.Seed, World.Dimension, start, end));
    }

    /// <summary>Moves a pin to another block and keeps it picked.</summary>
    public void MovePin(MapPin pin, BlockPos block)
    {
        _pinService.Move(pin, block);
        Selection = MapSelection.ForPin(pin);
    }

    /// <summary>Moves the Y level of the world by whole wheel steps, within the heights the world bar offers.</summary>
    public void StepSampleHeight(int steps)
        => _session.SampleHeight = Math.Clamp(_session.SampleHeight + steps * Constants.SampleHeightWheelStep, Constants.MinSampleHeight, Constants.MaxSampleHeight);

    public void SetHover(BlockPos? position)
    {
        HoverPosition = position;
        if (position is not { } block)
        {
            HoverText = string.Empty;
            return;
        }

        var location = BlockPosText.ToLocation(block);
        var text = DescribeSample(block) is { } sample ? $"{sample}  ·  {location}" : location;
        HoverText = MeasureStart is { } start ? $"{text}  ·  {BlockPosText.ToDistance(start, block)}" : text;
    }

    /// <summary>What the shown layer has at a block: the name of its biome or the value of its number, if that tile is loaded.</summary>
    private string? DescribeSample(BlockPos block)
    {
        var layer = _layerCatalog.GetInfo(Layer, World.Version, World.Dimension);
        if (layer.Values is not { } values)
            return GetBiomeName(block);

        return _tileService.TryGetCachedValue(Content, block.X, block.Z, out var stored)
            ? $"{layer.Name} {values.ToValue(stored).ToString(values.Format, CultureInfo.InvariantCulture)}"
            : null;
    }

    private string? GetBiomeName(BlockPos block)
        => _tileService.TryGetCachedBiome(Content, block.X, block.Z, out var biome) && biome != EngineConstants.NoBiomeId
            ? _biomeCatalog.GetDisplayName(World.Version, biome)
            : null;

    private MapCamera CenterOn(BlockPos block) => Camera with { CenterX = block.X + BlockMiddle, CenterZ = block.Z + BlockMiddle };

    private void Navigate(MapCamera target, bool animate)
    {
        if (NavigationRequested is { } handler)
            handler(new MapNavigation(target, animate));
        else
            Camera = target;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (World != _session.Key)
        {
            RecordJump();
            Selection = null;
        }

        ShowSession();
    }

    private void ShowSession()
    {
        World = _session.Key;
        SampleHeight = _session.SampleHeight;
        Layer = _layerCatalog.IsAvailable(_session.Layer, _session.Version, _session.Dimension) ? _session.Layer : LayerMode.Biomes;
        Octaves = _session.Octaves;
        ShowInHistory();
    }

    /// <summary>
    /// Makes the view shown now a step that Back returns to, before the map jumps elsewhere. All that changes before the app next
    /// waits for input counts as one jump, such as a found seed opened in its version together with its place.
    /// </summary>
    private void RecordJump()
    {
        if (!_isHistoryStarted || _isMovingInHistory || _isJumpUnderWay)
            return;

        _isJumpUnderWay = true;
        _history.Leave(GetShownView());
        Dispatcher.UIThread.Post(() => _isJumpUnderWay = false);
    }

    private void ShowInHistory()
    {
        if (_isHistoryStarted)
            _history.Show(GetShownView());
    }

    /// <summary>The view the map shows, which stays the old one while the session changes over to another world.</summary>
    private MapLink GetShownView()
    {
        var center = new BlockPos((int)Math.Floor(Camera.CenterX), (int)Math.Floor(Camera.CenterZ));
        return new MapLink(World.Seed, World.Version, World.Dimension, World.Flags.HasFlag(GeneratorFlags.LargeBiomes), center, Camera.BlocksPerPixel, Layer, SampleHeight, Octaves);
    }

    /// <summary>Opens a step of the history: its world and place, keeping the layer and the pins as they are now, without counting as a jump.</summary>
    private void OpenStep(MapLink view)
    {
        var flags = view.LargeBiomes ? GeneratorFlags.LargeBiomes : GeneratorFlags.None;
        var isSameWorld = World == new WorldKey(view.Seed, view.Version, view.Dimension, flags);
        _isMovingInHistory = true;
        try
        {
            Selection = null;
            _session.Version = view.Version;
            _session.LargeBiomes = view.LargeBiomes;
            _session.Dimension = view.Dimension;
            _session.Seed = view.Seed;
            Navigate(view.Camera, animate: isSameWorld);
        }
        finally
        {
            _isMovingInHistory = false;
        }
    }

    private void OnHistoryChanged()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
    }

    private void OnTileStateChanged()
    {
        if (Interlocked.Exchange(ref _stateUpdateQueued, 1) != 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _stateUpdateQueued, 0);
            IsGenerating = _tileService.PendingCount > 0;
        });
    }
}
