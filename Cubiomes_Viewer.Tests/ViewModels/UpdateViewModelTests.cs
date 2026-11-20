using Avalonia.Controls.Notifications;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.ProgressServ;
using Cubiomes_Viewer.Service.UpdateServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class UpdateViewModelTests : IDisposable
{
    private static readonly AppRelease _newer = new() { Version = "1.1.0" };

    private ServiceProvider? _services;

    public void Dispose() => _services?.Dispose();

    [Fact]
    public async Task CheckAtStart_NewerRelease_DownloadsItAndOffersARestart()
    {
        var updates = new FakeUpdateService { Newer = _newer };
        var viewModel = Create(updates);

        await viewModel.CheckAtStartAsync();

        Assert.Equal(_newer, updates.Downloaded);
        Assert.True(viewModel.IsReady);
        Assert.False(viewModel.CheckCommand.CanExecute(null));
        Assert.Equal("Version 1.1.0 is downloaded and installs when the app closes.", viewModel.StatusText);
        var announcement = Assert.Single(Notifications.Notifications);
        Assert.Equal(NotificationType.Information, announcement.Type);
        Assert.NotNull(announcement.OnClick);
        announcement.OnClick();
        Assert.Equal(1, updates.Restarts);
    }

    [Fact]
    public async Task CheckAtStart_TurnedOffInTheSettings_LooksForNothing()
    {
        var updates = new FakeUpdateService { Newer = _newer };
        var viewModel = Create(updates);
        viewModel.CheckAtStart = false;

        await viewModel.CheckAtStartAsync();

        Assert.Equal(0, updates.Checks);
        Assert.False(Settings.Current.Update.CheckAtStart);
    }

    [Fact]
    public async Task CheckAtStart_FeedUnreachable_SaysSoWithoutAnError()
    {
        var updates = new FakeUpdateService { Failure = "No such host is known." };
        var viewModel = Create(updates);

        await viewModel.CheckAtStartAsync();

        Assert.Empty(Notifications.Notifications);
        Assert.Equal("The check for updates failed: No such host is known.", viewModel.StatusText);
        Assert.False(viewModel.IsChecking);
        Assert.True(viewModel.CheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task Check_FeedUnreachable_ReportsAnError()
    {
        var viewModel = Create(new FakeUpdateService { Failure = "No such host is known." });

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(["No such host is known."], Notifications.Errors);
        Assert.Equal("The check for updates failed: No such host is known.", viewModel.StatusText);
    }

    [Fact]
    public async Task Check_RunningTheNewestVersion_SaysSoAndDownloadsNothing()
    {
        var updates = new FakeUpdateService();
        var viewModel = Create(updates);

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Null(updates.Downloaded);
        Assert.False(viewModel.IsReady);
        Assert.Equal("Cubiomes Viewer 1.0.0 is the newest version.", viewModel.StatusText);
        Assert.Empty(Notifications.Notifications);
    }

    [Fact]
    public async Task Check_NewerRelease_IsReadyWithoutAnnouncingIt()
    {
        var updates = new FakeUpdateService { Newer = _newer };
        var viewModel = Create(updates);

        await viewModel.CheckCommand.ExecuteAsync(null);
        viewModel.RestartCommand.Execute(null);

        Assert.True(viewModel.IsReady);
        Assert.Empty(Notifications.Notifications);
        Assert.Equal(1, updates.Restarts);
    }

    [Fact]
    public async Task CheckAtStart_OnRequest_OffersTheReleaseAndDownloadsItOnceAsked()
    {
        var updates = new FakeUpdateService { Style = UpdateStyle.OnRequest, Newer = _newer with { DownloadSize = 52_400_000 } };
        var viewModel = Create(updates);

        await viewModel.CheckAtStartAsync();

        Assert.Null(updates.Downloaded);
        Assert.True(viewModel.IsAvailable);
        Assert.False(viewModel.IsCheckShown);
        Assert.False(viewModel.CheckCommand.CanExecute(null));
        Assert.Equal("Version 1.1.0 is available (52 MB).", viewModel.StatusText);
        var announcement = Assert.Single(Notifications.Notifications);
        Assert.Equal("Cubiomes Viewer 1.1.0 is available (52 MB). Tap here to download and install it.", announcement.Message);
        Assert.NotNull(announcement.OnClick);

        announcement.OnClick();
        await TestWait.UntilAsync(() => updates.Restarts == 1, "The offered release was not handed to the installer.");

        Assert.Equal(updates.Newer, updates.Downloaded);
        Assert.True(viewModel.IsReady);
        Assert.False(viewModel.IsAvailable);
    }

    [Fact]
    public async Task DownloadAndInstall_OfferedRelease_HandsTheDownloadToTheInstaller()
    {
        var updates = new FakeUpdateService { Style = UpdateStyle.OnRequest, Newer = _newer };
        var viewModel = Create(updates);

        await viewModel.CheckCommand.ExecuteAsync(null);
        Assert.Null(updates.Downloaded);
        Assert.Equal("Version 1.1.0 is available.", viewModel.StatusText);
        await viewModel.DownloadAndInstallCommand.ExecuteAsync(null);

        Assert.Equal(_newer, updates.Downloaded);
        Assert.Equal(1, updates.Restarts);
        Assert.True(viewModel.IsReady);
        Assert.False(viewModel.IsAvailable);
        Assert.False(viewModel.DownloadAndInstallCommand.CanExecute(null));
        Assert.Equal("Install update", viewModel.RestartLabel);
        Assert.Equal("Version 1.1.0 is downloaded. Confirm the installation when the system asks.", viewModel.StatusText);
        Assert.Empty(Notifications.Notifications);
    }

    [Fact]
    public async Task DownloadAndInstall_WhileDownloading_FillsTheStatusStripAndEmptiesItAfter()
    {
        var gate = new TaskCompletionSource();
        var updates = new FakeUpdateService { Style = UpdateStyle.OnRequest, Newer = _newer, DownloadGate = gate };
        var viewModel = Create(updates);
        await viewModel.CheckCommand.ExecuteAsync(null);

        var downloading = viewModel.DownloadAndInstallCommand.ExecuteAsync(null);
        await TestWait.UntilAsync(() => Progress.Share is not null, "The download never showed how far it had come.");
        var halfway = Progress.Share;
        gate.SetResult();
        await downloading;

        Assert.Equal(0.5, halfway);
        Assert.Null(Progress.Share);
        Assert.Equal((1, 1), (Progress.Begun, Progress.Ended));
    }

    [Fact]
    public async Task DownloadAndInstall_DownloadFails_ReportsItAndKeepsTheOffer()
    {
        var updates = new FakeUpdateService { Style = UpdateStyle.OnRequest, Newer = _newer, DownloadFailure = "Connection reset." };
        var viewModel = Create(updates);

        await viewModel.CheckCommand.ExecuteAsync(null);
        await viewModel.DownloadAndInstallCommand.ExecuteAsync(null);

        Assert.Equal(["Connection reset."], Notifications.Errors);
        Assert.Equal("The download failed: Connection reset.", viewModel.StatusText);
        Assert.Equal(0, updates.Restarts);
        Assert.True(viewModel.IsAvailable);
        Assert.True(viewModel.DownloadAndInstallCommand.CanExecute(null));
        Assert.Equal((1, 1), (Progress.Begun, Progress.Ended));
    }

    [Fact]
    public async Task CopyThatCannotUpdate_ShowsItsVersionAndChecksNothing()
    {
        var updates = new FakeUpdateService { CanUpdate = false, CurrentVersion = "2.0.0", Newer = _newer };
        var viewModel = Create(updates);

        await viewModel.CheckAtStartAsync();

        Assert.False(viewModel.CanUpdate);
        Assert.False(viewModel.CheckCommand.CanExecute(null));
        Assert.Equal("Cubiomes Viewer 2.0.0", viewModel.VersionText);
        Assert.Equal(0, updates.Checks);
    }

    [Fact]
    public async Task ShowSettings_CheckTurnedOffBefore_ShowsItAndLooksForNothing()
    {
        var updates = new FakeUpdateService { Newer = _newer };
        var viewModel = Create(updates);
        Settings.Current.Update.CheckAtStart = false;

        viewModel.ShowSettings();
        await viewModel.CheckAtStartAsync();

        Assert.False(viewModel.CheckAtStart);
        Assert.Equal(0, updates.Checks);
    }

    private RecordingNotificationService Notifications => (RecordingNotificationService)Services.GetRequiredService<INotificationService>();

    private RecordingProgressService Progress => (RecordingProgressService)Services.GetRequiredService<IProgressService>();

    private ISettingsService Settings => Services.GetRequiredService<ISettingsService>();

    private ServiceProvider Services => _services ?? throw new InvalidOperationException("Create the view model first.");

    private UpdateViewModel Create(FakeUpdateService updates)
    {
        var services = new ServiceCollection();
        App.ConfigureServices(services);
        TestServices.UseIsolatedPlatform(services);
        services.AddSingleton<IUpdateService>(updates);
        _services = services.BuildServiceProvider();
        return _services.GetRequiredService<UpdateViewModel>();
    }
}
