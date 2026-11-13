using System.Globalization;

using Avalonia.Controls.Notifications;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.UpdateServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Shows the running version and, where the app can update itself, looks for a newer release at start or when asked,
/// downloads it and has it installed when the app closes or restarts.
/// </summary>
public sealed partial class UpdateViewModel : ViewModelBase
{
    public string Title { get; } = "Updates";
    public string CheckAtStartLabel { get; } = "Check for updates when the app starts";
    public string CheckAtStartHint { get; } = "A newer version downloads in the background and installs when the app closes.";
    public string CheckLabel { get; } = "Check now";
    public string RestartLabel { get; } = "Restart and update";

    public bool CanUpdate { get; }

    public string VersionText { get; }

    [ObservableProperty]
    public partial bool CheckAtStart { get; set; } = true;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    public partial bool IsChecking { get; set; }

    /// <summary>Whether a newer release is downloaded and waits for the app to close or restart.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(RestartCommand))]
    public partial bool IsReady { get; set; }

    private readonly IUpdateService _updates;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notifications;
    private readonly ILogger<UpdateViewModel> _logger;
    private bool _isShowingSettings;

    public UpdateViewModel(IUpdateService updates, ISettingsService settingsService, INotificationService notifications, ILogger<UpdateViewModel> logger) : base(notifications)
    {
        _updates = updates;
        _settingsService = settingsService;
        _notifications = notifications;
        _logger = logger;
        CanUpdate = updates.CanUpdate;
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

    /// <summary>Looks for a newer release and downloads it, when the settings ask for that at start. A check that fails only says so here.</summary>
    public Task CheckAtStartAsync() => CanUpdate && CheckAtStart ? RunGuardedAsync(() => UpdateAsync(atStart: true)) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckAsync() => RunGuardedAsync(() => UpdateAsync(atStart: false));

    [RelayCommand(CanExecute = nameof(IsReady))]
    private void Restart() => _updates.InstallAndRestart();

    private bool CanCheck() => CanUpdate && !IsChecking && !IsReady;

    partial void OnCheckAtStartChanged(bool value)
    {
        if (_isShowingSettings)
            return;

        _settingsService.Current.Update.CheckAtStart = value;
        _settingsService.RequestSave();
    }

    private async Task UpdateAsync(bool atStart)
    {
        IsChecking = true;
        try
        {
            StatusText = "Looking for a newer version…";
            if (await _updates.FindNewerReleaseAsync() is not { } release)
            {
                StatusText = $"{VersionText} is the newest version.";
                return;
            }

            await DownloadAsync(release);
            IsReady = true;
            StatusText = $"Version {release.Version} is downloaded and installs when the app closes.";
            if (atStart)
                AnnounceReady(release);
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

    /// <summary>Progress that arrives after the download completed is dropped, so that it cannot hide the status that follows.</summary>
    private async Task DownloadAsync(AppRelease release)
    {
        var isDownloading = true;
        var progress = new Progress<int>(percent =>
        {
            if (isDownloading)
                StatusText = string.Create(CultureInfo.InvariantCulture, $"Downloading version {release.Version}… {percent} %");
        });

        StatusText = $"Downloading version {release.Version}…";
        try
        {
            await _updates.DownloadAsync(release, progress);
        }
        finally
        {
            isDownloading = false;
        }
    }

    private void AnnounceReady(AppRelease release) => _notifications.ShowNotification(new Notification
    {
        Type = NotificationType.Information,
        Title = "Update ready",
        Message = $"{Constants.AppName} {release.Version} installs when you close the app. Click here to restart now.",
        OnClick = _updates.InstallAndRestart,
    });
}
