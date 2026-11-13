using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Engine.Service.AppDataServ;

/// <summary>
/// Keeps a copy of stored content that cannot be read before the app goes on without it, so that saving over the
/// original loses nothing, and lists the copies so the app can name them.
/// </summary>
public interface IUnreadableDataService
{
    /// <summary>The keys the copies were stored under since the app started.</summary>
    IReadOnlyList<string> CopyKeys { get; }

    /// <summary>Stores a copy of the unreadable content under a key of its own; the same content is copied once.</summary>
    Task KeepAsync(UnreadableDataException unreadable, CancellationToken cancellationToken = default);
}
