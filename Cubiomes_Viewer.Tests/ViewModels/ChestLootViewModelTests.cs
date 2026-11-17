using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Service.LootDescriptionServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class ChestLootViewModelTests : IDisposable
{
    private const int SearchSide = 4096;
    private static readonly BlockArea _searchArea = new(-SearchSide / 2, -SearchSide / 2, SearchSide, SearchSide);
    private static readonly JobPriority _priority = new(JobPriorityClass.Interactive);

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly ChestLootViewModel _chestLoot;
    private readonly MapViewModel _map;
    private readonly WorldSession _session;
    private readonly IMapStyleService _styleService;
    private readonly IComputeService _computeService;

    public ChestLootViewModelTests()
    {
        _chestLoot = _services.GetRequiredService<SelectionViewModel>().ChestLoot;
        _map = _services.GetRequiredService<MapViewModel>();
        _session = _services.GetRequiredService<WorldSession>();
        _styleService = _services.GetRequiredService<IMapStyleService>();
        _computeService = _services.GetRequiredService<IComputeService>();
        _session.Seed = 262;
        _session.Version = McVersion.V1_21_3;
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public async Task Selection_ShipwreckWhileChestLootIsOff_LeavesTheChestsOut()
    {
        var shipwreck = (await FindAsync(StructureType.Shipwreck))[0];

        _map.Selection = Select(StructureType.Shipwreck, shipwreck);

        Assert.False(_chestLoot.IsAvailable);
        Assert.False(_chestLoot.IsLoading);
        Assert.Empty(_chestLoot.Chests);
    }

    [AvaloniaFact]
    public async Task Selection_ShipwreckWhileChestLootIsOn_ListsItsChestsAndWhatTheyHold()
    {
        _map.ShowChestLoot = true;
        var shipwreck = (await FindAsync(StructureType.Shipwreck))[0];
        var expected = await DescribeAsync(StructureType.Shipwreck, shipwreck);

        _map.Selection = Select(StructureType.Shipwreck, shipwreck);
        Assert.True(_chestLoot.IsAvailable);
        await WaitForChestsAsync();

        Assert.Equal($"Chests ({expected.Count})", _chestLoot.Header);
        Assert.True(_chestLoot.HasChests);
        Assert.Equal(expected.Select(chest => chest.Title), _chestLoot.Chests.Select(chest => chest.Title));
        Assert.Equal(expected.SelectMany(chest => chest.Contents), _chestLoot.Chests.SelectMany(chest => chest.Contents));
        Assert.StartsWith("Chest 1  ·  X ", _chestLoot.Chests[0].Title, StringComparison.Ordinal);
        Assert.All(_chestLoot.Chests, chest => Assert.False(chest.IsExpanded));
        Assert.Empty(_chestLoot.Message);
        Assert.Empty(GetErrors());
    }

    [AvaloniaFact]
    public async Task ShowChestLoot_TurnedOnWhileAStructureIsPicked_ReadsItsChests()
    {
        var shipwreck = (await FindAsync(StructureType.Shipwreck))[0];
        _map.Selection = Select(StructureType.Shipwreck, shipwreck);

        _map.ShowChestLoot = true;
        await WaitForChestsAsync();

        Assert.True(_chestLoot.IsAvailable);
        Assert.NotEmpty(_chestLoot.Chests);

        _map.ShowChestLoot = false;

        Assert.False(_chestLoot.IsAvailable);
        Assert.Empty(_chestLoot.Chests);
    }

    [AvaloniaFact]
    public async Task Selection_StructureWithoutLoot_HasNoChestsToShow()
    {
        _map.ShowChestLoot = true;
        var village = (await FindAsync(StructureType.Village))[0];

        _map.Selection = Select(StructureType.Village, village);

        Assert.False(_chestLoot.IsAvailable);
        Assert.False(_chestLoot.IsLoading);
    }

    [AvaloniaFact]
    public async Task Selection_ChangedWhileChestsAreRead_ShowsOnlyTheLatestStructure()
    {
        _map.ShowChestLoot = true;
        var shipwrecks = await FindAsync(StructureType.Shipwreck);
        var expected = await DescribeAsync(StructureType.Shipwreck, shipwrecks[1]);

        _map.Selection = Select(StructureType.Shipwreck, shipwrecks[0]);
        _map.Selection = Select(StructureType.Shipwreck, shipwrecks[1]);
        await WaitForChestsAsync();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(expected.Select(chest => chest.Title), _chestLoot.Chests.Select(chest => chest.Title));
        Assert.Empty(GetErrors());
    }

    [AvaloniaFact]
    public async Task Selection_RuinedPortal_OpensItsOnlyChestAndSaysItsPlaceIsEstimated()
    {
        _map.ShowChestLoot = true;
        var portal = (await FindAsync(StructureType.RuinedPortal))[0];

        _map.Selection = Select(StructureType.RuinedPortal, portal);
        await WaitForChestsAsync();

        var chest = Assert.Single(_chestLoot.Chests);
        Assert.True(chest.IsExpanded);
        Assert.StartsWith("Chest  ·  X ", chest.Title, StringComparison.Ordinal);
        Assert.NotEmpty(_chestLoot.Note);
    }

    [AvaloniaFact]
    public async Task IsExpanded_Opened_StaysOpenForTheNextPick()
    {
        _map.ShowChestLoot = true;
        var shipwrecks = await FindAsync(StructureType.Shipwreck);
        _map.Selection = Select(StructureType.Shipwreck, shipwrecks[0]);
        _chestLoot.IsExpanded = true;

        _map.Selection = Select(StructureType.Shipwreck, shipwrecks[1]);
        await WaitForChestsAsync();

        Assert.True(_chestLoot.IsExpanded);
    }

    private MapSelection Select(StructureType type, BlockPos position)
        => MapSelection.ForStructure(type.ToString(), _styleService.GetStructureMarker(type), position, type);

    private async Task<BlockPos[]> FindAsync(StructureType type)
    {
        var found = await _computeService.RunAsync(new StructureAreaJob(new StructureAreaRequest(_map.World, type, _searchArea)), _priority, TestContext.Current.CancellationToken);
        Assert.True(found.Positions.Length >= 2, $"Too few structures of type {type} were found.");
        return found.Positions;
    }

    private async Task<IReadOnlyList<ChestDescription>> DescribeAsync(StructureType type, BlockPos position)
    {
        var loot = await _computeService.RunAsync(new StructureLootJob(_map.World, type, position), _priority, TestContext.Current.CancellationToken);
        return _services.GetRequiredService<ILootDescriptionService>().Describe(loot);
    }

    private Task WaitForChestsAsync() => TestWait.UntilAsync(() => !_chestLoot.IsLoading, "The chests were not read in time.");

    private IEnumerable<string?> GetErrors()
        => ((RecordingNotificationService)_services.GetRequiredService<INotificationService>()).Errors;
}
