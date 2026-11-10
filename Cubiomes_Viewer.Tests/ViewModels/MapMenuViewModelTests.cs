using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class MapMenuViewModelTests : IDisposable
{
    private static readonly BlockPos _position = new(-300, 1000);

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly MapMenuViewModel _menu;
    private readonly MapViewModel _map;
    private readonly WorldSession _session;

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
}
