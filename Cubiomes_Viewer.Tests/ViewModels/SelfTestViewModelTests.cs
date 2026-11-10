using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class SelfTestViewModelTests : IDisposable
{
    private const int SampleTileSize = 128;

    private readonly ServiceProvider _services = TestServices.Create();

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public async Task Run_WorkingGenerator_ShowsPassingChecksSampleTileAndBenchmark()
    {
        var selfTest = _services.GetRequiredService<SelfTestViewModel>();

        await selfTest.RunCommand.ExecuteAsync(null);

        var notifications = (RecordingNotificationService)_services.GetRequiredService<INotificationService>();
        Assert.Empty(notifications.Errors);
        Assert.NotEmpty(selfTest.Checks);
        Assert.All(selfTest.Checks, check => Assert.True(check.Passed, check.ToString()));
        Assert.False(selfTest.HasFailures);
        Assert.False(selfTest.IsRunning);
        Assert.Equal(SampleTileSize, selfTest.SampleTile?.PixelSize.Width);
        Assert.StartsWith("64 tiles at 1:4 in ", selfTest.BenchmarkText, StringComparison.Ordinal);
        Assert.EndsWith("on 1 worker)", selfTest.BenchmarkText, StringComparison.Ordinal);
    }
}
