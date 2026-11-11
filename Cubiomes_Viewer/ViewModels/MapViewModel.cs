using System.Collections.Frozen;
using System.ComponentModel;
using System.Globalization;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;

using EngineConstants = Cubiomes_Viewer.Engine.Data.Constants;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class MapViewModel : ViewModelBase
{
    private const double ButtonZoomStep = 2;

    public string GeneratingLabel { get; } = "Generating...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleText))]
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

    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

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

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    public string ScaleText => Camera.BlocksPerPixel >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"1 px = {Camera.BlocksPerPixel:0.##} blocks")
        : string.Create(CultureInfo.InvariantCulture, $"1 block = {1 / Camera.BlocksPerPixel:0.##} px");

    /// <summary>Raised when the map should travel to a camera; the view animates the move.</summary>
    public event Action<MapCamera>? NavigationRequested;

    private readonly WorldSession _session;
    private readonly IMapTileService _tileService;
    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ILayerCatalog _layerCatalog;
    private int _stateUpdateQueued;

    private MapContent Content => MapContent.For(World, Layer, SampleHeight, Octaves);

    public MapViewModel(WorldSession session, IMapTileService tileService, IBiomeCatalog biomeCatalog, ILayerCatalog layerCatalog, INotificationService notificationService) : base(notificationService)
    {
        _session = session;
        _tileService = tileService;
        _biomeCatalog = biomeCatalog;
        _layerCatalog = layerCatalog;

        ShowSession();
        session.PropertyChanged += OnSessionChanged;
        tileService.StateChanged += OnTileStateChanged;
    }

    [RelayCommand]
    public void GoToOrigin() => GoTo(0, 0);

    [RelayCommand]
    public void ZoomIn() => Navigate(Camera with { BlocksPerPixel = Camera.BlocksPerPixel / ButtonZoomStep });

    [RelayCommand]
    public void ZoomOut() => Navigate(Camera with { BlocksPerPixel = Camera.BlocksPerPixel * ButtonZoomStep });

    public void GoTo(double blockX, double blockZ) => Navigate(Camera with { CenterX = blockX, CenterZ = blockZ });

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
        HoverText = MeasureStart is { } start ? $"{text}  ·  {DescribeDistance(start, block)}" : text;
    }

    private static string DescribeDistance(BlockPos start, BlockPos end)
        => string.Create(CultureInfo.InvariantCulture, $"{start.DistanceTo(end):0.#} blocks from {start.X}, {start.Z}");

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

    private void Navigate(MapCamera target)
    {
        if (NavigationRequested is { } handler)
            handler(target);
        else
            Camera = target;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (World != _session.Key)
            Selection = null;

        ShowSession();
    }

    private void ShowSession()
    {
        World = _session.Key;
        SampleHeight = _session.SampleHeight;
        Layer = _layerCatalog.IsAvailable(_session.Layer, _session.Version, _session.Dimension) ? _session.Layer : LayerMode.Biomes;
        Octaves = _session.Octaves;
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
