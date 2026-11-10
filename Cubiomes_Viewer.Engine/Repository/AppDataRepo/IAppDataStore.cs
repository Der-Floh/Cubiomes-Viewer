namespace Cubiomes_Viewer.Engine.Repository.AppDataRepo;

/// <summary>Private, per-user storage for small named documents (settings, sessions, presets).</summary>
public interface IAppDataStore
{
    /// <summary>Returns the stored content, or <see langword="null"/> when nothing is stored under <paramref name="key"/>.</summary>
    Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default);

    Task WriteAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
