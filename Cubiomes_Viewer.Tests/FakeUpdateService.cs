using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.UpdateServ;

namespace Cubiomes_Viewer.Tests;

/// <summary>Finds a newer release, or fails as an unreachable release list would, and counts the checks.</summary>
internal sealed class FakeUpdateService : IUpdateService
{
    public string CurrentVersion { get; init; } = "1.0.0";
    public bool CanCheck { get; init; } = true;

    /// <summary>The release the next check finds, or <see langword="null"/> for none.</summary>
    public AppRelease? Newer { get; init; }

    /// <summary>Why the next check fails, or <see langword="null"/> when it succeeds.</summary>
    public string? Failure { get; init; }

    public int Checks { get; private set; }

    public Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default)
    {
        Checks++;
        return Failure is null ? Task.FromResult(Newer) : Task.FromException<AppRelease?>(new UpdateException(Failure, new HttpRequestException(Failure)));
    }
}
