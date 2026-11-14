using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Locate;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.LocateServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Finds the nearest structure, stronghold or biome around a block of the map, flies there and picks what it found. One search runs at a
/// time: another search, Stop or a change of the world ends the one before.
/// </summary>
public sealed partial class NearestSearchViewModel : ViewModelBase
{
    private const int BiomeCellScale = 4;

    public string StopLabel { get; } = "Stop";
    public string NoStrongholdsMessage { get; } = "This version has no strongholds.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    private readonly MapViewModel _map;
    private readonly WorldSession _session;
    private readonly ILocateService _locateService;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IBiomeCatalog _biomeCatalog;
    private readonly IMapStyleService _styleService;
    private readonly INotificationService _notifications;
    private CancellationTokenSource? _searching;

    public NearestSearchViewModel(MapViewModel map, WorldSession session, ILocateService locateService, IStructureCatalog structureCatalog, IBiomeCatalog biomeCatalog,
        IMapStyleService styleService, INotificationService notificationService) : base(notificationService)
    {
        _map = map;
        _session = session;
        _locateService = locateService;
        _structureCatalog = structureCatalog;
        _biomeCatalog = biomeCatalog;
        _styleService = styleService;
        _notifications = notificationService;

        map.PropertyChanged += OnMapChanged;
    }

    /// <summary>The structures of a version's dimension, with the strongholds in the Overworld, by name.</summary>
    public IReadOnlyList<NearestTarget> GetStructureTargets(McVersion version, Dimension dimension)
    {
        IEnumerable<NearestTarget> targets = _structureCatalog.GetAvailable(version, dimension).Select(info => new StructureTarget(info.Name, info.Type));
        if (dimension == Dimension.Overworld)
            targets = targets.Append(new StrongholdTarget(MapSelection.StrongholdName));

        return [.. targets.OrderBy(target => target.Name, StringComparer.CurrentCulture)];
    }

    /// <summary>The biomes of a version's dimension, by name.</summary>
    public IReadOnlyList<NearestTarget> GetBiomeTargets(McVersion version, Dimension dimension)
        =>
        [
            .. _biomeCatalog.GetBiomes(version, dimension)
                .Select(biome => new BiomeTarget(_biomeCatalog.GetDisplayName(version, biome) ?? biome.ToString(CultureInfo.InvariantCulture), biome))
                .OrderBy(target => target.Name, StringComparer.CurrentCulture),
        ];

    /// <summary>The marker the map draws a target with: a structure's or the strongholds'; <see langword="null"/> for a biome.</summary>
    public MapMarker? GetMarker(NearestTarget target) => target switch
    {
        StructureTarget structure => _styleService.GetStructureMarker(structure.Type),
        StrongholdTarget => _styleService.Stronghold,
        BiomeTarget => null,
        _ => throw new NotSupportedException($"Unsupported target: {target}"),
    };

    /// <summary>Searches outward from a block of the open world and shows what it finds, or says that nothing is in range.</summary>
    public Task FindAsync(NearestTarget target, BlockPos origin)
    {
        ArgumentNullException.ThrowIfNull(target);

        return RunGuardedAsync(() => SearchAsync(target, origin));
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    public void Stop() => _searching?.Cancel();

    private async Task SearchAsync(NearestTarget target, BlockPos origin)
    {
        _searching?.Cancel();
        using var searching = new CancellationTokenSource();
        _searching = searching;
        var (world, y) = (_map.World, _session.SampleHeight);
        var range = GetRange(target, world.Version);
        Title = $"Nearest {target.Name}";
        StatusText = string.Empty;
        IsRunning = true;
        try
        {
            var progress = new Progress<int>(covered =>
            {
                if (_searching == searching)
                    StatusText = string.Create(CultureInfo.InvariantCulture, $"Searched {covered:N0} of {range:N0} blocks");
            });
            var found = await LocateAsync(target, world, origin, y, range, progress, searching.Token);
            if (found is null)
                _notifications.ShowWarning(DescribeNotFound(target, origin, range, y));
            else
                Show(target, found, origin, y);
        }
        catch (OperationCanceledException) when (searching.IsCancellationRequested)
        {
            // Stopped, replaced by another search, or the world changed under it.
        }
        finally
        {
            if (_searching == searching)
            {
                _searching = null;
                IsRunning = false;
            }
        }
    }

    private Task<LocateResult?> LocateAsync(NearestTarget target, WorldKey world, BlockPos origin, int y, int range, IProgress<int> progress, CancellationToken cancellationToken)
        => target switch
        {
            StructureTarget structure => _locateService.FindStructureAsync(world, structure.Type, origin, range, progress, cancellationToken),
            StrongholdTarget => _locateService.FindStrongholdAsync(world, origin, cancellationToken),
            BiomeTarget biome => _locateService.FindBiomeAsync(world, biome.Biome, y, origin, range, progress, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported target: {target}"),
        };

    private int GetRange(NearestTarget target, McVersion version)
        => target is StructureTarget structure ? _locateService.GetStructureRange(structure.Type, version) : _locateService.BiomeRange;

    private string DescribeNotFound(NearestTarget target, BlockPos origin, int range, int y) => target switch
    {
        StrongholdTarget => NoStrongholdsMessage,
        BiomeTarget => string.Create(CultureInfo.InvariantCulture, $"No {target.Name} within {range:N0} blocks of {origin.X}, {origin.Z} at Y {y}."),
        _ => string.Create(CultureInfo.InvariantCulture, $"No {target.Name} within {range:N0} blocks of {origin.X}, {origin.Z}."),
    };

    /// <summary>Flies to what was found and picks it as the map would on a click, with its distance from where the search began.</summary>
    private void Show(NearestTarget target, LocateResult found, BlockPos origin, int y)
    {
        var distance = BlockPosText.ToDistance(origin, found.Position);
        var (selection, maxBlocksPerPixel) = target switch
        {
            StructureTarget structure => (MapSelection.ForStructure(structure.Name, _styleService.GetStructureMarker(structure.Type), found.Position, structure.Type, distance), double.PositiveInfinity),
            StrongholdTarget => (MapSelection.ForStronghold(found.Index ?? 0, _styleService.Stronghold, found.Position, distance), double.PositiveInfinity),
            BiomeTarget biome => (MapSelection.ForSpot(biome.Name, found.Position, string.Create(CultureInfo.InvariantCulture, $"{distance} at Y {y}")), GetBiomeCellZoom()),
            _ => throw new NotSupportedException($"Unsupported target: {target}"),
        };

        _map.FlyTo(found.Position, maxBlocksPerPixel);
        _map.Selection = selection;
    }

    /// <summary>The farthest zoom at which the map still draws biome cells of 4 blocks, where it shows biomes at all.</summary>
    private double GetBiomeCellZoom()
        => _map.Layer.ShowsBiomeMap ? TilePyramid.GetMaxBlocksPerPixel(TileMetrics.For(_map.World, _map.Layer), BiomeCellScale) : double.PositiveInfinity;

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.World))
            Stop();
    }
}
