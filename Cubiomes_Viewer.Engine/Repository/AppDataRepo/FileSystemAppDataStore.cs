using System.Collections.Concurrent;

using Cubiomes_Viewer.Engine.Data;

namespace Cubiomes_Viewer.Engine.Repository.AppDataRepo;

public sealed class FileSystemAppDataStore : IAppDataStore
{
    private const string TemporarySuffix = ".tmp";

    private readonly string _rootDirectory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writeGates = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemAppDataStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constants.AppDataFolderName))
    {
    }

    public FileSystemAppDataStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = rootDirectory;
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
    }

    /// <summary>Replaces the content of a key through a file of its own, one write of the key at a time.</summary>
    public async Task WriteAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        var gate = _writeGates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ReplaceAsync(path, content, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        File.Delete(GetPath(key));
        return Task.CompletedTask;
    }

    private async Task ReplaceAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}{TemporarySuffix}";
        Directory.CreateDirectory(_rootDirectory);
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary file is harmless; the write's own outcome is what the caller needs.
        }
    }

    private string GetPath(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (Path.GetFileName(key) != key)
            throw new ArgumentException($"The storage key '{key}' must be a plain file name without directories.", nameof(key));

        return Path.Combine(_rootDirectory, key);
    }
}
