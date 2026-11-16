using Avalonia.Platform.Storage;

using Cubiomes_Viewer.Service.TopLevelServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Service.StorageServ;

public sealed class StorageService : IStorageService
{
    private const string TemporarySuffix = ".tmp";
    private const int FileBufferSize = 1 << 16;

    public IStorageProvider StorageProvider => field ??= _topLevelProvider.GetTopLevel().StorageProvider;
    public bool CanOpenFiles => StorageProvider.CanOpen;
    public bool CanOpenFolders => StorageProvider.CanPickFolder;
    public bool CanSaveFiles => StorageProvider.CanSave;

    private readonly ITopLevelProvider _topLevelProvider;
    private readonly ILogger<StorageService> _logger;

    public StorageService(ITopLevelProvider topLevelProvider, ILogger<StorageService> logger)
    {
        _topLevelProvider = topLevelProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        _logger.LogDebug("Opening file picker: title={Title}", options.Title);
        var result = await StorageProvider.OpenFilePickerAsync(options);
        _logger.LogDebug("File picker returned {Count} file(s)", result.Count);
        return result;
    }

    public async Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options)
    {
        _logger.LogDebug("Opening folder picker: title={Title}", options.Title);
        var result = await StorageProvider.OpenFolderPickerAsync(options);
        _logger.LogDebug("Folder picker returned {Count} folder(s)", result.Count);
        return result;
    }

    public Task<string?> SaveFileAsync(FilePickerSaveOptions options, ReadOnlyMemory<byte> content)
        => SaveFileAsync(options, stream => stream.WriteAsync(content).AsTask());

    /// <summary>
    /// Writes a file the user picks. Where it has a local path, the content goes to a file beside it that replaces it once
    /// complete, so a failed or stopped write leaves a file that was there before as it was.
    /// </summary>
    public async Task<string?> SaveFileAsync(FilePickerSaveOptions options, Func<Stream, Task> writeAsync)
    {
        ArgumentNullException.ThrowIfNull(writeAsync);

        _logger.LogDebug("Opening save picker: title={Title}", options.Title);
        AvoidDoubledExtension(options);
        using var file = await StorageProvider.SaveFilePickerAsync(options);
        if (file is null)
        {
            _logger.LogDebug("Save picker was closed without choosing a file");
            return null;
        }

        if (file.TryGetLocalPath() is { } path)
            await WriteReplacingAsync(path, writeAsync);
        else
            await WriteInPlaceAsync(file, writeAsync);

        _logger.LogDebug("Saved {File}", file.Name);
        return file.Name;
    }

    /// <summary>Drops the default extension from the suggested name on Android, where the save picker adds it once more.</summary>
    private static void AvoidDoubledExtension(FilePickerSaveOptions options)
    {
        if (!OperatingSystem.IsAndroid() || options.SuggestedFileName is not { } name || options.DefaultExtension is not { Length: > 0 } extension)
            return;

        var suffix = extension.StartsWith('.') ? extension : $".{extension}";
        if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            options.SuggestedFileName = name[..^suffix.Length];
    }

    internal async Task WriteReplacingAsync(string path, Func<Stream, Task> writeAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(writeAsync);

        var temporaryPath = $"{path}.{Guid.NewGuid():N}{TemporarySuffix}";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileBufferSize, useAsync: true))
                await writeAsync(stream);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private async Task WriteInPlaceAsync(IStorageFile file, Func<Stream, Task> writeAsync)
    {
        var isComplete = false;
        try
        {
            await using (var stream = await file.OpenWriteAsync())
                await writeAsync(stream);
            isComplete = true;
        }
        finally
        {
            if (!isComplete)
                await TryDeleteAsync(file);
        }
    }

    /// <summary>Removes a temporary file that was not moved into place; failing to do so must not hide why.</summary>
    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not remove the temporary file {File}", path);
        }
    }

    /// <summary>Removes a file left incomplete; failing to do so must not hide why it is incomplete.</summary>
    private async Task TryDeleteAsync(IStorageFile file)
    {
        try
        {
            await file.DeleteAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not remove the incomplete file {File}", file.Name);
        }
    }

    public async Task<byte[]?> OpenFileAsync(FilePickerOpenOptions options)
    {
        var files = await OpenFilePickerAsync(options);
        try
        {
            if (files.Count == 0)
                return null;

            await using var stream = await files[0].OpenReadAsync();
            using var content = new MemoryStream();
            await stream.CopyToAsync(content);
            _logger.LogDebug("Read {Bytes} bytes from {File}", content.Length, files[0].Name);
            return content.ToArray();
        }
        finally
        {
            foreach (var file in files)
                file.Dispose();
        }
    }

    public Task<IStorageFile?> TryGetFileFromPathAsync(string absolutePath)
        => StorageProvider.TryGetFileFromPathAsync(new Uri(absolutePath));
}
