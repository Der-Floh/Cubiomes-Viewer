using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.UpdateServ;

/// <summary>
/// Finds, downloads and installs newer releases of the app where it can update itself, as a desktop app installed with
/// its setup. A downloaded release installs once the app closes.
/// </summary>
public interface IUpdateService
{
    /// <summary>The version that runs.</summary>
    string CurrentVersion { get; }

    /// <summary>Whether this copy of the app can update itself; the web version and development builds cannot.</summary>
    bool CanUpdate { get; }

    /// <summary>Looks for a release newer than the running version.</summary>
    /// <returns>The newest release, or <see langword="null"/> when the running version is the newest.</returns>
    /// <exception cref="UpdateException">The releases cannot be reached or read.</exception>
    Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default);

    /// <summary>Downloads the release that <see cref="FindNewerReleaseAsync"/> found last, to be installed when the app closes.</summary>
    /// <param name="progress">Receives how much is downloaded, from 0 to 100.</param>
    /// <exception cref="UpdateException">The release cannot be downloaded.</exception>
    Task DownloadAsync(AppRelease release, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Has the downloaded release, if there is one, installed once the app has closed; the app calls this as it closes.</summary>
    void InstallOnExit();

    /// <summary>Closes the app, installs the downloaded release and starts it.</summary>
    /// <exception cref="InvalidOperationException">No release is downloaded.</exception>
    void InstallAndRestart();
}
