using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.UpdateServ;

namespace Cubiomes_Viewer.Tests;

/// <summary>Offers a release, or fails as an unreachable feed would, and records what the app asked of it.</summary>
internal sealed class FakeUpdateService : IUpdateService
{
    public string CurrentVersion { get; init; } = "1.0.0";
    public bool CanUpdate { get; init; } = true;
    public UpdateStyle Style { get; init; } = UpdateStyle.Background;

    /// <summary>The release the next check finds, or <see langword="null"/> for none.</summary>
    public AppRelease? Newer { get; init; }

    /// <summary>Why the next check fails, or <see langword="null"/> when it succeeds.</summary>
    public string? Failure { get; init; }

    /// <summary>Why downloads fail, or <see langword="null"/> when they succeed.</summary>
    public string? DownloadFailure { get; init; }

    /// <summary>Holds a download halfway until it is set, or <see langword="null"/> to let downloads run through.</summary>
    public TaskCompletionSource? DownloadGate { get; init; }

    public int Checks { get; private set; }
    public AppRelease? Downloaded { get; private set; }
    public int InstallsOnExit { get; private set; }
    public int Restarts { get; private set; }

    public Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default)
    {
        Checks++;
        return Failure is null ? Task.FromResult(Newer) : Task.FromException<AppRelease?>(new UpdateException(Failure, new HttpRequestException(Failure)));
    }

    public async Task DownloadAsync(AppRelease release, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (DownloadFailure is not null)
            throw new UpdateException(DownloadFailure, new HttpRequestException(DownloadFailure));

        progress?.Report(50);
        if (DownloadGate is { } gate)
            await gate.Task;
        progress?.Report(100);
        Downloaded = release;
    }

    public void InstallOnExit() => InstallsOnExit++;

    public void InstallAndRestart() => Restarts++;
}
