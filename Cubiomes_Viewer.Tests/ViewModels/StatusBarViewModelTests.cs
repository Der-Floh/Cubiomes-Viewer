using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using Cubiomes_Viewer.Service.ProgressServ;
using Cubiomes_Viewer.Service.UpdateServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class StatusBarViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create(services => services.AddSingleton<IUpdateService>(new FakeUpdateService { CurrentVersion = "1.2.3" }));
    private readonly StatusBarViewModel _statusBar;
    private readonly IProgressService _progress;

    public StatusBarViewModelTests()
    {
        _statusBar = _services.GetRequiredService<StatusBarViewModel>();
        _progress = _services.GetRequiredService<IProgressService>();
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void Version_IsTheVersionOfTheApp()
        => Assert.Equal("v1.2.3", _statusBar.VersionText);

    [AvaloniaFact]
    public void Progress_OfARunningAction_IsShownUntilTheActionEnds()
    {
        var action = _progress.Begin();

        action.Report(0.25);
        var whileRunning = (_statusBar.IsProgressShown, _statusBar.ProgressShare);
        action.Dispose();

        Assert.Equal((true, 0.25), whileRunning);
        Assert.Equal((false, 0d), (_statusBar.IsProgressShown, _statusBar.ProgressShare));
    }

    [AvaloniaFact]
    public async Task Progress_ReportedOnAnotherThread_IsShownOnceTheUiThreadRuns()
    {
        using var action = _progress.Begin();

        await Task.Run(() => action.Report(0.6), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((true, 0.6), (_statusBar.IsProgressShown, _statusBar.ProgressShare));
    }
}
