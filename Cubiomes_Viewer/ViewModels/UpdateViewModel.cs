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
/// Shows the running version and, where the app can update itself, looks for a newer release at start or when asked.
/// On the desktop the release downloads right away and installs when the app closes or restarts; on Android it is
/// offered, downloads once asked for and goes to the system's installer.
/// </summary>
public sealed partial class UpdateViewModel : ViewModelBase
{
    private const double BytesPerMegabyte = 1_000_000;

    public string Title { get; } = "Updates";
    public string CheckAtStartLabel { get; } = "Check for updates when the app starts";
    public string CheckAtStartHint { get; }
    public string CheckLabel { get; } = "Check now";
    public string DownloadLabel { get; } = "Download and install";
    public string RestartLabel { get; }

    public bool CanUpdate { get; }

    public string VersionText { get; }

    [ObservableProperty]
    public partial bool CheckAtStart { get; set; } = true;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>Whether a check or a download is under way.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(DownloadAndInstallCommand))]
    public partial bool IsChecking { get; set; }

    /// <summary>Whether a newer release is found and waits for the user to download it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCheckShown))]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(DownloadAndInstallCommand))]
    public partial bool IsAvailable { get; set; }

    /// <summary>Whether a newer release is downloaded and waits to be installed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCheckShown))]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(RestartCommand))]
    public partial bool IsReady { get; set; }

    /// <summary>Whether Check now is offered, which it is not while a found release waits to be downloaded or installed.</summary>
    public bool IsCheckShown => !IsReady && !IsAvailable;

    private readonly IUpdateService _updates;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notifications;
    private readonly ILogger<UpdateViewModel> _logger;
    private readonly bool _isOnRequest;
    private AppRelease? _available;
    private bool _isShowingSettings;

    public UpdateViewModel(IUpdateService updates, ISettingsService settingsService, INotificationService notifications, ILogger<UpdateViewModel> logger) : base(notifications)
    {
        _updates = updates;
        _settingsService = settingsService;
        _notifications = notifications;
        _logger = logger;
        _isOnRequest = updates.Style == UpdateStyle.OnRequest;
        CanUpdate = updates.CanUpdate;
        VersionText = $"{Constants.AppName} {updates.CurrentVersion}";
        CheckAtStartHint = _isOnRequest
            ? "A newer version is announced, and downloads once you ask for it."
            : "A newer version downloads in the background and installs when the app closes.";
        RestartLabel = _isOnRequest ? "Install update" : "Restart and update";
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

    /// <summary>Looks for a newer release, when the settings ask for that at start. A check that fails only says so here.</summary>
    public Task CheckAtStartAsync() => CanUpdate && CheckAtStart ? RunGuardedAsync(() => UpdateAsync(atStart: true)) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckAsync() => RunGuardedAsync(() => UpdateAsync(atStart: false));

    /// <summary>Downloads the offered release and hands it to the system's installer.</summary>
    [RelayCommand(CanExecute = nameof(CanDownload))]
    private Task DownloadAndInstallAsync() => RunGuardedAsync(DownloadOfferedAsync);

    [RelayCommand(CanExecute = nameof(IsReady))]
    private void Restart() => _updates.InstallAndRestart();

    private bool CanCheck() => CanUpdate && !IsChecking && !IsReady && !IsAvailable;

    private bool CanDownload() => IsAvailable && !IsChecking;

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

            if (_isOnRequest)
                Offer(release, atStart);
            else
                await DownloadInBackgroundAsync(release, atStart);
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

    private async Task DownloadInBackgroundAsync(AppRelease release, bool atStart)
    {
        await DownloadAsync(release);
        IsReady = true;
        StatusText = $"Version {release.Version} is downloaded and installs when the app closes.";
        if (atStart)
            AnnounceReady(release);
    }

    private void Offer(AppRelease release, bool atStart)
    {
        _available = release;
        IsAvailable = true;
        StatusText = $"Version {release.Version} is available{FormatSize(release)}.";
        if (atStart)
            AnnounceAvailable(release);
    }

    private async Task DownloadOfferedAsync()
    {
        var release = _available ?? throw new InvalidOperationException("No release is offered.");
        IsChecking = true;
        try
        {
            await DownloadAsync(release);
        }
        catch (UpdateException ex)
        {
            StatusText = $"The download failed: {ex.Message}";
            throw;
        }
        finally
        {
            IsChecking = false;
        }

        _available = null;
        IsAvailable = false;
        IsReady = true;
        StatusText = $"Version {release.Version} is downloaded. Confirm the installation when the system asks.";
        _updates.InstallAndRestart();
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

    private static string FormatSize(AppRelease release)
        => release.DownloadSize is { } bytes ? string.Create(CultureInfo.InvariantCulture, $" ({bytes / BytesPerMegabyte:0} MB)") : string.Empty;

    private void AnnounceReady(AppRelease release) => _notifications.ShowNotification(new Notification
    {
        Type = NotificationType.Information,
        Title = "Update ready",
        Message = $"{Constants.AppName} {release.Version} installs when you close the app. Click here to restart now.",
        OnClick = _updates.InstallAndRestart,
    });

    private void AnnounceAvailable(AppRelease release) => _notifications.ShowNotification(new Notification
    {
        Type = NotificationType.Information,
        Title = "Update available",
        Message = $"{Constants.AppName} {release.Version} is available{FormatSize(release)}. Tap here to download and install it.",
        OnClick = () => DownloadAndInstallCommand.Execute(null),
    });
}
