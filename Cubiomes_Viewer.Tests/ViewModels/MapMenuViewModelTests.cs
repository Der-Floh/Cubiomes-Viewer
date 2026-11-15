using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LocateServ;
using Cubiomes_Viewer.Engine.Service.PinServ;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class MapMenuViewModelTests : IDisposable
{
    private static readonly BlockPos _position = new(-300, 1000);
    private static readonly byte[] _image = [137, 80, 78, 71];

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly MapMenuViewModel _menu;
    private readonly MapViewModel _map;
    private readonly WorldSession _session;

    private IMapStyleService Styles => _services.GetRequiredService<IMapStyleService>();

    public MapMenuViewModelTests()
    {
        _menu = _services.GetRequiredService<MapMenuViewModel>();
        _map = _services.GetRequiredService<MapViewModel>();
        _session = _services.GetRequiredService<WorldSession>();
        _menu.Position = _position;
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void Position_Set_IsShownInTheMenuLabels()
    {
        _session.Seed = 262;

        Assert.Equal("Copy coordinates (-300, 1000)", _menu.CopyCoordinatesLabel);
        Assert.Equal("Copy chunk (-19, 62)", _menu.CopyChunkLabel);
        Assert.Equal("Copy region file (r.-1.1.mca)", _menu.CopyRegionFileLabel);
        Assert.Equal("Copy seed (262)", _menu.CopySeedLabel);
    }

    [AvaloniaFact]
    public void PlacePin_FromTheMenu_PutsAPinOnItsBlockAndPicksIt()
    {
        _session.Seed = 262;

        _menu.PlacePinCommand.Execute(null);

        var pin = Assert.Single(_services.GetRequiredService<IPinService>().GetPins(262, Dimension.Overworld));
        Assert.Equal((_position.X, _position.Z), (pin.X, pin.Z));
        Assert.Same(pin, _map.Selection?.Pin);
    }

    [AvaloniaFact]
    public async Task CopyCommands_Executed_PutTheShownTextOnTheClipboardAndConfirm()
    {
        _session.Seed = 262;

        await _menu.CopyCoordinatesCommand.ExecuteAsync(null);
        await _menu.CopyTeleportCommand.ExecuteAsync(null);
        await _menu.CopyChunkCommand.ExecuteAsync(null);
        await _menu.CopyRegionFileCommand.ExecuteAsync(null);
        await _menu.CopySeedCommand.ExecuteAsync(null);

        var clipboard = (RecordingClipboardService)_services.GetRequiredService<IClipboardService>();
        var notifications = (RecordingNotificationService)_services.GetRequiredService<INotificationService>();
        Assert.Equal(["-300, 1000", "/tp @s -300 ~ 1000", "-19, 62", "r.-1.1.mca", "262"], clipboard.Texts);
        Assert.Empty(notifications.Errors);
        Assert.Equal(clipboard.Texts, notifications.Notifications.Select(notification => notification.Message));
    }

    [AvaloniaFact]
    public async Task CopyLink_Executed_CopiesALinkCentredOnTheClickedBlockAtTheMapsZoom()
    {
        _session.Seed = 262;
        _session.Version = McVersion.V1_21_3;
        _map.Camera = new MapCamera(0, 0, 2);

        await _menu.CopyLinkCommand.ExecuteAsync(null);

        var clipboard = (RecordingClipboardService)_services.GetRequiredService<IClipboardService>();
        Assert.Equal($"{RecordingAppAddressService.TestLinkBase}?seed=262&version=1.21.3&dimension=overworld&x=-300&z=1000&scale=2", Assert.Single(clipboard.Texts));
    }

    [AvaloniaFact]
    public void ToggleMeasuring_Executed_StartsAtTheMenuPositionAndStopsAgain()
    {
        _menu.ToggleMeasuringCommand.Execute(null);
        Assert.Equal(_position, _map.MeasureStart);
        Assert.Equal("Stop measuring", _menu.MeasureLabel);

        _menu.ToggleMeasuringCommand.Execute(null);
        Assert.Null(_map.MeasureStart);
        Assert.Equal("Measure distance from here", _menu.MeasureLabel);
    }

    [AvaloniaFact]
    public void Hover_WhileMeasuring_ShowsTheDistanceFromTheStart()
    {
        _map.MeasureStart = new BlockPos(10, -20);

        _map.SetHover(new BlockPos(40, 20));

        Assert.EndsWith("50 blocks from 10, -20", _map.HoverText, StringComparison.Ordinal);
        Assert.Equal(new BlockPos(40, 20), _map.HoverPosition);
    }

    [AvaloniaFact]
    public void Hover_WhenMeasuringStops_DropsTheDistanceAtOnce()
    {
        _map.MeasureStart = new BlockPos(10, -20);
        _map.SetHover(new BlockPos(40, 20));

        _menu.StopMeasuringCommand.Execute(null);

        Assert.DoesNotContain("blocks from", _map.HoverText, StringComparison.Ordinal);
        Assert.Contains("X 40, Z 20", _map.HoverText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task SaveImage_FileChosen_WritesTheMapImageToIt()
    {
        _session.Seed = 262;
        _menu.ImageSource = new FixedImageSource(_image);

        await _menu.SaveImageCommand.ExecuteAsync(null);

        var storage = (MemoryStorageService)_services.GetRequiredService<IStorageService>();
        var notifications = (RecordingNotificationService)_services.GetRequiredService<INotificationService>();
        var version = _services.GetRequiredService<IVersionCatalog>().GetName(_session.Version);
        var fileName = $"cubiomes-262-{version}-overworld.png".ToLowerInvariant();
        Assert.Equal(_image, storage.SavedContent);
        Assert.Equal(fileName, storage.LastSaveOptions?.SuggestedFileName);
        Assert.Equal([fileName], notifications.Notifications.Select(notification => notification.Message));
        Assert.Empty(notifications.Errors);
    }

    [AvaloniaFact]
    public async Task SaveImage_PickerClosed_WritesNothing()
    {
        var storage = (MemoryStorageService)_services.GetRequiredService<IStorageService>();
        storage.IsSaveCancelled = true;
        _menu.ImageSource = new FixedImageSource(_image);

        await _menu.SaveImageCommand.ExecuteAsync(null);

        Assert.Null(storage.SavedContent);
        Assert.Empty(((RecordingNotificationService)_services.GetRequiredService<INotificationService>()).Notifications);
    }

    [AvaloniaFact]
    public void OpenShadowSeed_LayeredVersion_OpensTheShadowAndRemembersIt()
    {
        _session.Version = McVersion.V1_16_5;
        _session.Seed = 262;

        Assert.True(_menu.OpenShadowSeedCommand.CanExecute(null));
        _menu.OpenShadowSeedCommand.Execute(null);

        var shadow = SeedMath.GetShadowSeed(262);
        Assert.Equal(shadow, _session.Seed);
        Assert.Equal(shadow, _services.GetRequiredService<ISeedHistoryService>().Seeds[0]);
    }

    [AvaloniaFact]
    public void OpenShadowSeed_NoiseBasedVersion_IsNotOffered()
    {
        _session.Version = McVersion.V1_21_3;

        Assert.False(_menu.OpenShadowSeedCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void StructureTargets_Overworld_ListItsStructuresWithTheStrongholdsByName()
    {
        var names = _menu.StructureTargets.Select(target => target.Name).ToList();

        Assert.True(_menu.HasStructureTargets);
        Assert.Contains("Village", names);
        Assert.Contains(MapSelection.StrongholdName, names);
        Assert.DoesNotContain(_menu.StructureTargets, target => target.Target is StructureTarget { Type: StructureType.Fortress });
        Assert.Equal(names.Order(StringComparer.CurrentCulture), names);
    }

    [AvaloniaFact]
    public void Targets_Shown_CarryTheMarkersTheMapDrawsThemWith()
    {
        var styleService = _services.GetRequiredService<IMapStyleService>();

        Assert.Same(styleService.GetStructureMarker(StructureType.Village), _menu.StructureTargets.Single(target => target.Name == "Village").Marker);
        Assert.Same(styleService.Stronghold, _menu.StructureTargets.Single(target => target.Target is StrongholdTarget).Marker);
        Assert.All(_menu.BiomeTargets, target => Assert.Same(styleService.GetBiomeMarker(Assert.IsType<BiomeTarget>(target.Target).Biome), target.Marker));
    }

    [AvaloniaFact]
    public void StructureTargets_DimensionChanged_ListTheNethersStructures()
    {
        _session.Dimension = Dimension.Nether;

        var types = _menu.StructureTargets.Select(target => target.Target).OfType<StructureTarget>().Select(target => target.Type).ToList();
        Assert.Contains(StructureType.Fortress, types);
        Assert.Contains(StructureType.Bastion, types);
        Assert.DoesNotContain(StructureType.Village, types);
        Assert.DoesNotContain(_menu.StructureTargets, target => target.Target is StrongholdTarget);
    }

    [AvaloniaFact]
    public void BiomeTargets_Version_ListItsBiomesByName()
    {
        var biomes = _services.GetRequiredService<IBiomeCatalog>().GetBiomes(_session.Version, Dimension.Overworld);

        var names = _menu.BiomeTargets.Select(target => target.Name).ToList();

        Assert.True(_menu.HasBiomeTargets);
        Assert.Equal(biomes.Count, names.Count);
        Assert.Contains("Mushroom Fields", names);
        Assert.Equal(names.Order(StringComparer.CurrentCulture), names);
    }

    [AvaloniaFact]
    public async Task FindTarget_Executed_SearchesFromTheClickedBlock()
    {
        _session.Seed = 262;
        var locate = _services.GetRequiredService<ILocateService>();
        var range = locate.GetStructureRange(StructureType.Village, _session.Version);
        var expected = await locate.FindStructureAsync(_map.World, StructureType.Village, _position, range, cancellationToken: TestContext.Current.CancellationToken);

        await _menu.StructureTargets.Single(target => target.Target is StructureTarget { Type: StructureType.Village }).FindCommand.ExecuteAsync(null);

        Assert.NotNull(expected);
        Assert.Equal(expected.Position, _map.Selection?.Position);
        Assert.Equal(BlockPosText.ToDistance(_position, expected.Position), _map.Selection?.Note);
    }

    [AvaloniaFact]
    public void OtherDimensionLabel_Position_NamesTheOtherDimensionAndItsBlock()
    {
        Assert.Equal("Open in the Nether (-38, 125)", _menu.OtherDimensionLabel);

        _session.Dimension = Dimension.Nether;

        Assert.Equal("Open in the Overworld (-2396, 8004)", _menu.OtherDimensionLabel);
    }

    [AvaloniaFact]
    public void OpenOtherDimension_InTheOverworld_OpensTheNetherBlockAtAnEighthAndMarksIt()
    {
        _map.Camera = new MapCamera(-250, 900, 2);

        _menu.OpenOtherDimensionCommand.Execute(null);

        Assert.Equal(Dimension.Nether, _session.Dimension);
        Assert.Equal(new MapCamera(-37.5, 125.5, 2), _map.Camera);
        Assert.Equal(MapSelection.ForSpot("From the Overworld", new BlockPos(-38, 125), "Overworld X -300, Z 1000", Styles.GetDimensionMarker(Dimension.Overworld),
            Styles.GetPinLook(MarkerNames.Of(Dimension.Overworld))), _map.Selection);
    }

    [AvaloniaFact]
    public void OpenOtherDimension_InTheNether_OpensTheMiddleOfItsEightBlocks()
    {
        _session.Dimension = Dimension.Nether;
        _menu.Position = new BlockPos(12, -7);

        _menu.OpenOtherDimensionCommand.Execute(null);

        Assert.Equal(Dimension.Overworld, _session.Dimension);
        Assert.Equal(MapSelection.ForSpot("From the Nether", new BlockPos(100, -52), "Nether X 12, Z -7", Styles.GetDimensionMarker(Dimension.Nether),
            Styles.GetPinLook(MarkerNames.Of(Dimension.Nether))), _map.Selection);
    }

    [AvaloniaFact]
    public void OpenOtherDimension_FarOutInTheNether_StaysInsideTheWorldBorder()
    {
        _session.Dimension = Dimension.Nether;
        _menu.Position = new BlockPos(10_000_000, -10_000_000);

        _menu.OpenOtherDimensionCommand.Execute(null);

        Assert.Equal(new BlockPos(30_000_000, -30_000_000), _map.Selection?.Position);
    }

    [AvaloniaFact]
    public void OpenOtherDimension_InTheEnd_IsNotOffered()
    {
        _session.Dimension = Dimension.End;

        Assert.False(_menu.CanOpenOtherDimension);
        Assert.False(_menu.OpenOtherDimensionCommand.CanExecute(null));
    }

    private sealed class FixedImageSource(byte[] image) : IMapImageSource
    {
        public byte[] CapturePng() => image;
    }
}
