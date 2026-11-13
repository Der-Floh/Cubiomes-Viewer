using System.Text.Json;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.UpdateServ;

using Microsoft.Extensions.Logging;

using Velopack;
using Velopack.Sources;

namespace Cubiomes_Viewer.Desktop.Service.UpdateServ;

/// <summary>
/// Updates a copy installed with its Velopack setup from the releases on GitHub, or from the folder or address in
/// <see cref="FeedVariable"/>, so that release packages can be tried before they are published.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    public const string FeedVariable = "CUBIOMES_VIEWER_UPDATE_FEED";

    private readonly UpdateManager _manager;
    private readonly ILogger<VelopackUpdateService> _logger;
    private UpdateInfo? _found;
    private UpdateInfo? _downloaded;
    private bool _isInstallStarted;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger;
        _manager = CreateManager();
    }

    public string CurrentVersion => _manager.CurrentVersion?.ToString() ?? AppVersion.Current;
    public bool CanUpdate => _manager.IsInstalled;

    public async Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfCannotUpdate();
        try
        {
            _found = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            throw new UpdateException(ex.Message, ex);
        }

        _logger.LogInformation("Newest release: {Version}", _found?.TargetFullRelease.Version.ToString() ?? CurrentVersion);
        return _found is null ? null : new AppRelease { Version = _found.TargetFullRelease.Version.ToString() };
    }

    public async Task DownloadAsync(AppRelease release, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ThrowIfCannotUpdate();
        if (_found is not { } update || update.TargetFullRelease.Version.ToString() != release.Version)
            throw new InvalidOperationException("Only the release found last can be downloaded.");

        try
        {
            await _manager.DownloadUpdatesAsync(update, percent => progress?.Report(percent), cancellationToken);
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            throw new UpdateException(ex.Message, ex);
        }

        _downloaded = update;
    }

    public void InstallOnExit()
    {
        if (_downloaded is not { } update || _isInstallStarted)
            return;

        _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
        _isInstallStarted = true;
    }

    public void InstallAndRestart()
    {
        var update = _downloaded ?? throw new InvalidOperationException("No release is downloaded.");
        if (!_isInstallStarted)
        {
            _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: true);
            _isInstallStarted = true;
        }

        Dispatcher.UIThread.Post(CloseApp);
    }

    private void ThrowIfCannotUpdate()
    {
        if (!CanUpdate)
            throw new InvalidOperationException("This copy of the app was not installed with its setup and cannot update itself.");
    }

    private static UpdateManager CreateManager()
    {
        var feed = Environment.GetEnvironmentVariable(FeedVariable);
        return string.IsNullOrWhiteSpace(feed)
            ? new UpdateManager(new GithubSource(Constants.RepositoryUrl, accessToken: null, prerelease: false))
            : new UpdateManager(feed);
    }

    /// <summary>Failures of the network, the disk or the feed, which a later check may not run into; a timeout counts, a cancellation does not.</summary>
    private static bool IsUnreachable(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException or IOException or JsonException || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>Closes the app the way its window does, so that it stores what it has to before the updater takes over.</summary>
    private static void CloseApp()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
