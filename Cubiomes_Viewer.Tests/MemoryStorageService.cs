using Avalonia.Platform.Storage;

using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.Tests;

/// <summary>Stands in for the file pickers: a saved file is kept in memory.</summary>
internal sealed class MemoryStorageService : IStorageService
{
    public IStorageProvider StorageProvider => throw new NotSupportedException("The tests have no storage provider.");
    public bool CanOpenFiles => false;
    public bool CanOpenFolders => false;
    public bool CanSaveFiles => true;

    /// <summary>Makes the save picker behave as if the user closed it without choosing a file.</summary>
    public bool IsSaveCancelled { get; set; }

    public FilePickerSaveOptions? LastSaveOptions { get; private set; }
    public byte[]? SavedContent { get; private set; }

    public Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
        => Task.FromResult<IReadOnlyList<IStorageFile>>([]);

    public Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options)
        => Task.FromResult<IReadOnlyList<IStorageFolder>>([]);

    public Task<string?> SaveFileAsync(FilePickerSaveOptions options, ReadOnlyMemory<byte> content)
    {
        LastSaveOptions = options;
        if (IsSaveCancelled)
            return Task.FromResult<string?>(null);

        SavedContent = content.ToArray();
        return Task.FromResult(options.SuggestedFileName);
    }

    public Task<IStorageFile?> TryGetFileFromPathAsync(string absolutePath) => Task.FromResult<IStorageFile?>(null);
}
