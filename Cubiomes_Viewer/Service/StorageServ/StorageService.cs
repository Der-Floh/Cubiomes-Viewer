using Avalonia.Platform.Storage;

using Cubiomes_Viewer.Service.TopLevelServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Service.StorageServ;

public sealed class StorageService : IStorageService
{
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

    public async Task<string?> SaveFileAsync(FilePickerSaveOptions options, ReadOnlyMemory<byte> content)
    {
        _logger.LogDebug("Opening save picker: title={Title}", options.Title);
        using var file = await StorageProvider.SaveFilePickerAsync(options);
        if (file is null)
        {
            _logger.LogDebug("Save picker was closed without choosing a file");
            return null;
        }

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(content);
        _logger.LogDebug("Saved {Bytes} bytes to {File}", content.Length, file.Name);
        return file.Name;
    }

    public async Task<string?> SaveFileAsync(FilePickerSaveOptions options, Func<Stream, Task> writeAsync)
    {
        ArgumentNullException.ThrowIfNull(writeAsync);

        _logger.LogDebug("Opening save picker: title={Title}", options.Title);
        using var file = await StorageProvider.SaveFilePickerAsync(options);
        if (file is null)
        {
            _logger.LogDebug("Save picker was closed without choosing a file");
            return null;
        }

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

        _logger.LogDebug("Saved {File}", file.Name);
        return file.Name;
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
