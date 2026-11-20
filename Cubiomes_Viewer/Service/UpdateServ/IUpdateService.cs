using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.UpdateServ;

/// <summary>
/// Finds out whether a release newer than the running version is out. The app does not update itself; a newer release
/// is pointed out with its page, which offers its downloads.
/// </summary>
public interface IUpdateService
{
    /// <summary>The version that runs.</summary>
    string CurrentVersion { get; }

    /// <summary>Whether this copy of the app looks for newer releases; the web version, which always runs the newest release, and development builds do not.</summary>
    bool CanCheck { get; }

    /// <summary>Looks for a release newer than the running version.</summary>
    /// <returns>The newest release, or <see langword="null"/> when the running version is the newest.</returns>
    /// <exception cref="UpdateException">The releases cannot be reached or read.</exception>
    Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default);
}
