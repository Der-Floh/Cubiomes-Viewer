using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.UpdateServ;

/// <summary>For copies of the app that do not look for newer releases, such as the web version, which always runs the newest release.</summary>
public sealed class NoUpdateService : IUpdateService
{
    public string CurrentVersion => AppVersion.Current;
    public bool CanCheck => false;

    public Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default) => Task.FromResult<AppRelease?>(null);
}
