using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.UpdateServ;

namespace Cubiomes_Viewer.Tests;

/// <summary>Offers a release, or fails as an unreachable feed would, and records what the app asked of it.</summary>
internal sealed class FakeUpdateService : IUpdateService
{
    public string CurrentVersion { get; init; } = "1.0.0";
    public bool CanUpdate { get; init; } = true;

    /// <summary>The release the next check finds, or <see langword="null"/> for none.</summary>
    public AppRelease? Newer { get; init; }

    /// <summary>Why the next check fails, or <see langword="null"/> when it succeeds.</summary>
    public string? Failure { get; init; }

    public int Checks { get; private set; }
    public AppRelease? Downloaded { get; private set; }
    public int InstallsOnExit { get; private set; }
    public int Restarts { get; private set; }

    public Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default)
    {
        Checks++;
        return Failure is null ? Task.FromResult(Newer) : Task.FromException<AppRelease?>(new UpdateException(Failure, new HttpRequestException(Failure)));
    }

    public Task DownloadAsync(AppRelease release, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(50);
        progress?.Report(100);
        Downloaded = release;
        return Task.CompletedTask;
    }

    public void InstallOnExit() => InstallsOnExit++;

    public void InstallAndRestart() => Restarts++;
}
