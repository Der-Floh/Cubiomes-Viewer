using Avalonia.Controls.Notifications;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.BrowserServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.UpdateServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Shows the running version and, where the app can tell, looks for a newer release at start or when asked. A newer
/// release found at start is pointed out by a notice that opens its page; the app does not update itself.
/// </summary>
public sealed partial class UpdateViewModel : ViewModelBase
{
    public string Title { get; } = "Updates";
    public string CheckAtStartLabel { get; } = "Tell me about new versions at start";
    public string CheckAtStartHint { get; } = "At start the app asks GitHub for the newest release and, when it is newer, shows a notice that opens its downloads.";
    public string CheckLabel { get; } = "Check now";
    public string OpenReleaseLabel { get; } = "Open release";

    public bool CanCheck { get; }

    public string VersionText { get; }

    [ObservableProperty]
    public partial bool CheckAtStart { get; set; } = true;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    public partial bool IsChecking { get; set; }

    /// <summary>The newer release the last check found, if it found one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewerRelease))]
    [NotifyCanExecuteChangedFor(nameof(OpenReleaseCommand))]
    public partial AppRelease? NewerRelease { get; set; }

    public bool HasNewerRelease => NewerRelease is not null;

    private readonly IUpdateService _updates;
    private readonly ISettingsService _settingsService;
    private readonly IBrowserService _browser;
    private readonly INotificationService _notifications;
    private readonly ILogger<UpdateViewModel> _logger;
    private bool _isShowingSettings;

    public UpdateViewModel(IUpdateService updates, ISettingsService settingsService, IBrowserService browser, INotificationService notifications, ILogger<UpdateViewModel> logger) : base(notifications)
    {
        _updates = updates;
        _settingsService = settingsService;
        _browser = browser;
        _notifications = notifications;
        _logger = logger;
        CanCheck = updates.CanCheck;
        VersionText = $"{Constants.AppName} {updates.CurrentVersion}";
    }

    /// <summary>Shows the stored settings; called once they are loaded.</summary>
    public void ShowSettings()
    {
        _isShowingSettings = true;
        try
        {
            CheckAtStart = _settingsService.Current.Update.CheckAtStart;
        }
        finally
        {
            _isShowingSettings = false;
        }
    }

    /// <summary>Looks for a newer release when the settings ask for that at start, and points it out. A check that fails only says so here.</summary>
    public Task CheckAtStartAsync() => CanCheck && CheckAtStart ? RunGuardedAsync(() => LookForNewerReleaseAsync(atStart: true)) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private Task CheckAsync() => RunGuardedAsync(() => LookForNewerReleaseAsync(atStart: false));

    [RelayCommand(CanExecute = nameof(HasNewerRelease))]
    private Task OpenReleaseAsync() => RunGuardedAsync(() => NewerRelease is { } release ? _browser.OpenAsync(release.PageUrl) : Task.CompletedTask);

    private bool CanCheckNow() => CanCheck && !IsChecking;

    partial void OnCheckAtStartChanged(bool value)
    {
        if (_isShowingSettings)
            return;

        _settingsService.Current.Update.CheckAtStart = value;
        _settingsService.RequestSave();
    }

    private async Task LookForNewerReleaseAsync(bool atStart)
    {
        IsChecking = true;
        try
        {
            StatusText = "Looking for a newer version…";
            NewerRelease = await _updates.FindNewerReleaseAsync();
            StatusText = NewerRelease is { } release ? $"Version {release.Version} is out." : $"{VersionText} is the newest version.";
            if (atStart && NewerRelease is { } newer)
                PointOut(newer);
        }
        catch (UpdateException ex)
        {
            StatusText = $"The check for updates failed: {ex.Message}";
            if (!atStart)
                throw;

            _logger.LogWarning(ex, "The check for a newer version at start failed");
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void PointOut(AppRelease release)
        => _notifications.ShowUntilClosed(new UpdateNoticeViewModel(release, _updates.CurrentVersion, _browser, TurnOffNotices, _notifications), NotificationType.Information);

    private void TurnOffNotices() => CheckAtStart = false;
}
