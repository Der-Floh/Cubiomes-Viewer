using Avalonia.Controls.Notifications;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.BrowserServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.UpdateServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class UpdateViewModelTests : IDisposable
{
    private static readonly AppRelease _newer = new() { Version = "1.1.0", PageUrl = new Uri("https://github.com/Der-Floh/Cubiomes-Viewer/releases/tag/v1.1.0") };

    private ServiceProvider? _services;

    public void Dispose() => _services?.Dispose();

    [Fact]
    public async Task CheckAtStart_NewerRelease_ShowsANoticeUntilItIsClosed()
    {
        var viewModel = Create(new FakeUpdateService { Newer = _newer });

        await viewModel.CheckAtStartAsync();

        var (content, type) = Assert.Single(Notifications.Notices);
        var notice = Assert.IsType<UpdateNoticeViewModel>(content);
        Assert.Equal(NotificationType.Information, type);
        Assert.Equal("Update available", notice.Title);
        Assert.Equal("Cubiomes Viewer 1.1.0 is out; you have 1.0.0.", notice.Message);
        Assert.Empty(Notifications.Notifications);
        Assert.Equal("Version 1.1.0 is out.", viewModel.StatusText);
        Assert.True(viewModel.HasNewerRelease);
    }

    [Fact]
    public async Task Notice_Open_OpensThePageOfTheRelease()
    {
        var notice = await ShowNoticeAsync(Create(new FakeUpdateService { Newer = _newer }));

        await notice.OpenCommand.ExecuteAsync(null);

        Assert.Equal([_newer.PageUrl], Browser.Pages);
    }

    [Fact]
    public async Task Notice_DontShowAgain_TurnsTheNoticesOffAndKeepsThemOff()
    {
        var updates = new FakeUpdateService { Newer = _newer };
        var viewModel = Create(updates);
        var notice = await ShowNoticeAsync(viewModel);

        notice.DontShowAgainCommand.Execute(null);
        await viewModel.CheckAtStartAsync();

        Assert.False(viewModel.CheckAtStart);
        Assert.False(Settings.Current.Update.CheckAtStart);
        Assert.Equal(1, updates.Checks);
        Assert.Single(Notifications.Notices);
        Assert.Empty(Browser.Pages);
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
    public async Task CheckAtStart_ReleasesUnreachable_SaysSoWithoutAnError()
    {
        var viewModel = Create(new FakeUpdateService { Failure = "No such host is known." });

        await viewModel.CheckAtStartAsync();

        Assert.Empty(Notifications.Notifications);
        Assert.Empty(Notifications.Notices);
        Assert.Equal("The check for updates failed: No such host is known.", viewModel.StatusText);
        Assert.False(viewModel.IsChecking);
        Assert.True(viewModel.CheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task Check_ReleasesUnreachable_ReportsAnError()
    {
        var viewModel = Create(new FakeUpdateService { Failure = "No such host is known." });

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(["No such host is known."], Notifications.Errors);
        Assert.Equal("The check for updates failed: No such host is known.", viewModel.StatusText);
    }

    [Fact]
    public async Task Check_RunningTheNewestVersion_SaysSo()
    {
        var viewModel = Create(new FakeUpdateService());

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal("Cubiomes Viewer 1.0.0 is the newest version.", viewModel.StatusText);
        Assert.False(viewModel.HasNewerRelease);
        Assert.False(viewModel.OpenReleaseCommand.CanExecute(null));
        Assert.Empty(Notifications.Notifications);
    }

    [Fact]
    public async Task Check_NewerRelease_OffersItsPageWithoutANotice()
    {
        var viewModel = Create(new FakeUpdateService { Newer = _newer });

        await viewModel.CheckCommand.ExecuteAsync(null);
        await viewModel.OpenReleaseCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Notices);
        Assert.Equal("Version 1.1.0 is out.", viewModel.StatusText);
        Assert.Equal([_newer.PageUrl], Browser.Pages);
    }

    [Fact]
    public async Task CopyThatCannotCheck_ShowsItsVersionAndChecksNothing()
    {
        var updates = new FakeUpdateService { CanCheck = false, CurrentVersion = "2.0.0", Newer = _newer };
        var viewModel = Create(updates);

        await viewModel.CheckAtStartAsync();

        Assert.False(viewModel.CanCheck);
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

    private RecordingBrowserService Browser => (RecordingBrowserService)Services.GetRequiredService<IBrowserService>();

    private ISettingsService Settings => Services.GetRequiredService<ISettingsService>();

    private ServiceProvider Services => _services ?? throw new InvalidOperationException("Create the view model first.");

    private UpdateViewModel Create(FakeUpdateService updates)
    {
        _services = TestServices.Create(services => services.AddSingleton<IUpdateService>(updates));
        return _services.GetRequiredService<UpdateViewModel>();
    }

    private async Task<UpdateNoticeViewModel> ShowNoticeAsync(UpdateViewModel viewModel)
    {
        await viewModel.CheckAtStartAsync();
        return Assert.IsType<UpdateNoticeViewModel>(Assert.Single(Notifications.Notices).Content);
    }
}
