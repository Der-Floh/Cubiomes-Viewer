using Cubiomes_Viewer.Engine.Data;

namespace Cubiomes_Viewer.Engine.Repository.AppDataRepo;

public sealed class FileSystemAppDataStore : IAppDataStore
{
    private const string TemporarySuffix = ".tmp";

    private readonly string _rootDirectory;

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

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        var temporaryPath = path + TemporarySuffix;

        Directory.CreateDirectory(_rootDirectory);
        await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        File.Delete(GetPath(key));
        return Task.CompletedTask;
    }

    private string GetPath(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (Path.GetFileName(key) != key)
            throw new ArgumentException($"The storage key '{key}' must be a plain file name without directories.", nameof(key));

        return Path.Combine(_rootDirectory, key);
    }
}
