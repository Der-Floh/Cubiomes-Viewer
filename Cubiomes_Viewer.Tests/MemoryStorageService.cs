using Avalonia.Platform.Storage;

using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.Tests;

/// <summary>Stands in for the file pickers: a saved file is kept in memory.</summary>
internal sealed class MemoryStorageService : IStorageService
{
    public IStorageProvider StorageProvider => throw new NotSupportedException("The tests have no storage provider.");
    public bool CanOpenFiles => true;
    public bool CanOpenFolders => false;
    public bool CanSaveFiles => true;

    /// <summary>Makes the save picker behave as if the user closed it without choosing a file.</summary>
    public bool IsSaveCancelled { get; set; }

    public FilePickerSaveOptions? LastSaveOptions { get; private set; }
    public byte[]? SavedContent { get; private set; }

    /// <summary>What the open picker hands back, as if the user chose a file with it; nothing behaves as if they closed the picker.</summary>
    public byte[]? ContentToOpen { get; set; }

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

    public async Task<string?> SaveFileAsync(FilePickerSaveOptions options, Func<Stream, Task> writeAsync)
    {
        LastSaveOptions = options;
        if (IsSaveCancelled)
            return null;

        using var stream = new MemoryStream();
        SavedContent = null;
        await writeAsync(stream);
        SavedContent = stream.ToArray();
        return options.SuggestedFileName;
    }

    public Task<byte[]?> OpenFileAsync(FilePickerOpenOptions options) => Task.FromResult(ContentToOpen);

    public Task<IStorageFile?> TryGetFileFromPathAsync(string absolutePath) => Task.FromResult<IStorageFile?>(null);
}
