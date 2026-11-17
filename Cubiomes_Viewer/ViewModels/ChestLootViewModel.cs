using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.LootServ;
using Cubiomes_Viewer.Service.LootDescriptionServ;
using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// The containers of the picked structure and what they hold, while the setting to show chest loot is on. They are read as soon
/// as a structure is picked, so the header can count them; the list stays open or closed from one pick to the next.
/// </summary>
public sealed partial class ChestLootViewModel : ViewModelBase
{
    private const string ChestsName = "Chests";

    private static readonly JobPriority _priority = new(JobPriorityClass.Interactive);

    public string LoadingLabel { get; } = "Reading the chests...";
    public string UnknownLabel { get; } = "The chests of this structure cannot be read.";
    public string NoChestsLabel { get; } = "This structure has no chests.";

    /// <summary>Whether the picked structure's chests can be listed: the setting is on and the generator can read them.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string Header { get; set; } = ChestsName;

    /// <summary>Why no container is listed, if none is.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasChests { get; set; }

    /// <summary>What the generator only estimates about these containers; empty where it knows them.</summary>
    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    public ObservableCollection<ChestViewModel> Chests { get; } = [];

    private readonly MapViewModel _map;
    private readonly IComputeService _computeService;
    private readonly ILootCatalog _lootCatalog;
    private readonly ILootDescriptionService _descriptionService;
    private CancellationTokenSource? _loading;

    public ChestLootViewModel(MapViewModel map, IComputeService computeService, ILootCatalog lootCatalog, ILootDescriptionService descriptionService,
        INotificationService notificationService) : base(notificationService)
    {
        _map = map;
        _computeService = computeService;
        _lootCatalog = lootCatalog;
        _descriptionService = descriptionService;

        map.PropertyChanged += OnMapChanged;
    }

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapViewModel.Selection) or nameof(MapViewModel.ShowChestLoot))
            ShowChests();
    }

    private void ShowChests()
    {
        _loading?.Cancel();
        _loading = null;
        Chests.Clear();
        HasChests = false;
        Header = ChestsName;
        Message = string.Empty;
        Note = string.Empty;
        IsLoading = false;

        var world = _map.World;
        var picked = _map.ShowChestLoot ? _map.Selection : null;
        IsAvailable = picked?.Structure is { } type && _lootCatalog.HasLoot(type, world.Version);
        if (IsAvailable && picked is { Structure: { } structure })
            _ = RunGuardedAsync(() => LoadAsync(structure, picked.Position, world));
    }

    private async Task LoadAsync(StructureType type, BlockPos position, WorldKey world)
    {
        var loading = new CancellationTokenSource();
        _loading = loading;
        IsLoading = true;
        try
        {
            var loot = await _computeService.RunAsync(new StructureLootJob(world, type, position), _priority, loading.Token);
            if (_loading == loading)
                ShowLoot(loot, type, world.Version);
        }
        catch (OperationCanceledException)
        {
            // Another structure was picked, or chest loot was turned off, before the chests were read.
        }
        finally
        {
            if (_loading == loading)
            {
                _loading = null;
                IsLoading = false;
            }

            loading.Dispose();
        }
    }

    private void ShowLoot(StructureLoot loot, StructureType type, McVersion version)
    {
        if (!loot.IsKnown)
        {
            Message = UnknownLabel;
            return;
        }

        var chests = _descriptionService.Describe(loot);
        foreach (var chest in chests)
            Chests.Add(new ChestViewModel(chest, isExpanded: chests.Count == 1));

        HasChests = chests.Count > 0;
        Header = string.Create(CultureInfo.InvariantCulture, $"{ChestsName} ({chests.Count})");
        Message = chests.Count == 0 ? NoChestsLabel : string.Empty;
        Note = _descriptionService.DescribeAccuracy(type, version) ?? string.Empty;
    }
}
