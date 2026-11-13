using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.UpdateServ;

/// <summary>For copies of the app that do not update themselves, such as the web version, which always runs the newest release.</summary>
public sealed class NoUpdateService : IUpdateService
{
    private const string CannotUpdateMessage = "This copy of the app does not update itself.";

    public string CurrentVersion => AppVersion.Current;
    public bool CanUpdate => false;

    public Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default) => Task.FromResult<AppRelease?>(null);

    public Task DownloadAsync(AppRelease release, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(CannotUpdateMessage);

    public void InstallOnExit()
    {
    }

    public void InstallAndRestart() => throw new InvalidOperationException(CannotUpdateMessage);
}
