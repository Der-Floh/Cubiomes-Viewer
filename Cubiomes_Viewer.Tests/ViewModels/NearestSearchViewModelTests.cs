using Avalonia.Controls.Notifications;
using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.LocateServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class NearestSearchViewModelTests : IDisposable
{
    private const int Plains = 1;
    private static readonly BlockPos _origin = new(300, -200);

    private ServiceProvider _services = TestServices.Create();

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public async Task FindAsync_Village_FliesThereAndPicksItWithItsDistance()
    {
        var (nearest, map) = Open();
        var expected = await Locate.FindStructureAsync(map.World, StructureType.Village, _origin, Locate.GetStructureRange(StructureType.Village, map.World.Version), cancellationToken: TestContext.Current.CancellationToken);

        await nearest.FindAsync(new StructureTarget("Village", StructureType.Village), _origin);

        Assert.NotNull(expected);
        var marker = _services.GetRequiredService<IMapStyleService>().GetStructureMarker(StructureType.Village);
        Assert.Equal(MapSelection.ForStructure("Village", marker, expected.Position, StructureType.Village, BlockPosText.ToDistance(_origin, expected.Position)), map.Selection);
        Assert.Equal(new MapCamera(expected.Position.X + 0.5, expected.Position.Z + 0.5, MapCamera.DefaultBlocksPerPixel), map.Camera);
        Assert.False(nearest.IsRunning);
        Assert.Empty(Notifications.Notifications);
    }

    [AvaloniaFact]
    public async Task FindAsync_Stronghold_PicksItUnderTheNameTheMapGivesIt()
    {
        var (nearest, map) = Open();

        await nearest.FindAsync(new StrongholdTarget(MapSelection.StrongholdName), _origin);

        var landmarks = await _services.GetRequiredService<IComputeService>().RunAsync(new LandmarksJob(map.World), new JobPriority(JobPriorityClass.Interactive), TestContext.Current.CancellationToken);
        var picked = Assert.IsType<MapSelection>(map.Selection);
        var index = Array.IndexOf(landmarks.Strongholds, picked.Position);
        Assert.Equal(MapSelection.ForStronghold(index, _services.GetRequiredService<IMapStyleService>().Stronghold, picked.Position, picked.Note), picked);
        Assert.Equal(landmarks.Strongholds.Min(stronghold => stronghold.SquaredDistanceTo(_origin)), picked.Position.SquaredDistanceTo(_origin));
    }

    [AvaloniaFact]
    public async Task FindAsync_Biome_ZoomsInUntilItsCellsShowAndRingsTheCell()
    {
        var (nearest, map) = Open();
        var session = _services.GetRequiredService<WorldSession>();
        session.Version = McVersion.V1_18_2;
        session.SampleHeight = 63;
        map.Camera = new MapCamera(_origin.X, _origin.Z, 64);

        await nearest.FindAsync(new BiomeTarget("Plains", Plains), _origin);

        var picked = Assert.IsType<MapSelection>(map.Selection);
        Assert.True(picked.IsSpot);
        Assert.Equal("Plains", picked.Name);
        Assert.Equal($"{BlockPosText.ToDistance(_origin, picked.Position)} at Y 63", picked.Note);
        Assert.Equal(TilePyramid.GetMaxBlocksPerPixel(TileMetrics.For(map.World, LayerMode.Biomes), 4), map.Camera.BlocksPerPixel);
        Assert.Empty(Notifications.Notifications);
    }

    [AvaloniaFact]
    public async Task FindAsync_NothingInRange_WarnsAndLeavesTheMap()
    {
        UseLocate(new FakeLocateService(waitsUntilCancelled: false));
        var (nearest, map) = Open();
        var camera = map.Camera;

        await nearest.FindAsync(new StructureTarget("Village", StructureType.Village), _origin);

        var warning = Assert.Single(Notifications.Notifications);
        Assert.Equal(NotificationType.Warning, warning.Type);
        Assert.Equal("No Village within 54,400 blocks of 300, -200.", warning.Message);
        Assert.Equal(camera, map.Camera);
        Assert.Null(map.Selection);
        Assert.False(nearest.IsRunning);
    }

    [AvaloniaFact]
    public async Task Stop_WhileFinding_EndsTheSearchWithoutAWarning()
    {
        UseLocate(new FakeLocateService(waitsUntilCancelled: true));
        var (nearest, _) = Open();

        var finding = nearest.FindAsync(new BiomeTarget("Plains", Plains), _origin);
        Assert.True(nearest.IsRunning);
        Assert.Equal("Nearest Plains", nearest.Title);
        nearest.StopCommand.Execute(null);
        await finding;

        Assert.False(nearest.IsRunning);
        Assert.False(nearest.StopCommand.CanExecute(null));
        Assert.Empty(Notifications.Notifications);
    }

    [AvaloniaFact]
    public async Task WorldChanged_WhileFinding_StopsTheSearch()
    {
        UseLocate(new FakeLocateService(waitsUntilCancelled: true));
        var (nearest, _) = Open();

        var finding = nearest.FindAsync(new StructureTarget("Village", StructureType.Village), _origin);
        _services.GetRequiredService<WorldSession>().Seed = 263;
        await finding;

        Assert.False(nearest.IsRunning);
        Assert.Empty(Notifications.Notifications);
    }

    private ILocateService Locate => _services.GetRequiredService<ILocateService>();

    private RecordingNotificationService Notifications => (RecordingNotificationService)_services.GetRequiredService<INotificationService>();

    private (NearestSearchViewModel Nearest, MapViewModel Map) Open()
    {
        _services.GetRequiredService<WorldSession>().Seed = 262;
        return (_services.GetRequiredService<NearestSearchViewModel>(), _services.GetRequiredService<MapViewModel>());
    }

    private void UseLocate(ILocateService locate)
    {
        _services.Dispose();
        _services = TestServices.Create(services => services.AddSingleton(locate));
    }
}
