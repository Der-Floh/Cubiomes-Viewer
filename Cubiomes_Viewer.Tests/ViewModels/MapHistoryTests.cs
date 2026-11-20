using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class MapHistoryTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();
    private readonly MainViewModel _main;
    private readonly WorldSession _session;

    public MapHistoryTests()
    {
        _main = _services.GetRequiredService<MainViewModel>();
        _session = _services.GetRequiredService<WorldSession>();
    }

    public void Dispose() => _services.Dispose();

    private MapViewModel Map => _main.Map;

    [AvaloniaFact]
    public async Task GoBack_AfterASeedWasOpenedInAnotherVersionWithItsPlace_ReturnsToTheWorldAndPlaceBefore()
    {
        await _main.Initialization;
        var (seed, version) = (_session.Seed, _session.Version);
        Map.Camera = new MapCamera(100.5, -40.5, 4);
        var atStart = Map.CanGoBack;

        _session.Version = McVersion.V1_16_5;
        _session.Seed = seed + 1;
        Map.GoTo(500, 500);
        Dispatcher.UIThread.RunJobs();
        var afterTheJump = (Map.CanGoBack, Map.CanGoForward);
        Map.GoBackCommand.Execute(null);

        Assert.False(atStart);
        Assert.Equal((true, false), afterTheJump);
        Assert.Equal((seed, version), (_session.Seed, _session.Version));
        Assert.Equal(new MapCamera(100.5, -40.5, 4), Map.Camera);
        Assert.Equal((false, true), (Map.CanGoBack, Map.CanGoForward));
    }

    [AvaloniaFact]
    public async Task GoForward_AfterGoingBack_OpensTheJumpAgain()
    {
        await _main.Initialization;
        var seed = _session.Seed;
        _session.Seed = seed + 1;
        Map.GoTo(500, 500);
        Dispatcher.UIThread.RunJobs();
        Map.GoBackCommand.Execute(null);

        Map.GoForwardCommand.Execute(null);

        Assert.Equal(seed + 1, _session.Seed);
        Assert.Equal((500, 500), ((int)Math.Floor(Map.Camera.CenterX), (int)Math.Floor(Map.Camera.CenterZ)));
        Assert.False(Map.CanGoForward);
    }

    [AvaloniaFact]
    public async Task Camera_MovedByHand_StaysWithinTheStep()
    {
        await _main.Initialization;

        Map.Camera = new MapCamera(10, 10, 4);
        Dispatcher.UIThread.RunJobs();
        Map.Camera = new MapCamera(900, -300, 16);
        Map.ZoomInCommand.Execute(null);

        Assert.False(Map.CanGoBack);
    }

    [AvaloniaFact]
    public async Task GoBack_AfterTwoJumps_StepsBackOneJumpAtATime()
    {
        await _main.Initialization;
        var start = (int)Math.Floor(Map.Camera.CenterX);
        Map.GoTo(10, 10);
        Dispatcher.UIThread.RunJobs();
        Map.GoTo(20, 20);
        Dispatcher.UIThread.RunJobs();

        Map.GoBackCommand.Execute(null);
        var once = (int)Math.Floor(Map.Camera.CenterX);
        Map.GoBackCommand.Execute(null);

        Assert.Equal(10, once);
        Assert.Equal(start, (int)Math.Floor(Map.Camera.CenterX));
        Assert.False(Map.CanGoBack);
    }

    [AvaloniaFact]
    public async Task GoBack_KeepsTheLayerShownNow()
    {
        await _main.Initialization;
        Map.GoTo(10, 10);
        Dispatcher.UIThread.RunJobs();
        _session.Layer = LayerMode.Temperature;

        Map.GoBackCommand.Execute(null);

        Assert.Equal(LayerMode.Temperature, _session.Layer);
    }
}
