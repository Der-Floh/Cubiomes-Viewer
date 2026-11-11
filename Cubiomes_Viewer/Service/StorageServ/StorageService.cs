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

    public Task<IStorageFile?> TryGetFileFromPathAsync(string absolutePath)
        => StorageProvider.TryGetFileFromPathAsync(new Uri(absolutePath));
}
