using Avalonia.Platform.Storage;

namespace Cubiomes_Viewer.Service.StorageServ;

public interface IStorageService
{
    IStorageProvider StorageProvider { get; }
    bool CanOpenFiles { get; }
    bool CanOpenFolders { get; }
    bool CanSaveFiles { get; }
    Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options);
    Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options);

    /// <summary>
    /// Lets the user choose where to save a file and writes the content there.
    /// Returns the name of the saved file, or <see langword="null"/> when they cancel.
    /// </summary>
    Task<string?> SaveFileAsync(FilePickerSaveOptions options, ReadOnlyMemory<byte> content);

    /// <summary>
    /// Lets the user choose where to save a file and lets <paramref name="writeAsync"/> write it there, which suits
    /// content too large to hold in memory. A file that could not be written to the end is removed again.
    /// Returns the name of the saved file, or <see langword="null"/> when they cancel.
    /// </summary>
    Task<string?> SaveFileAsync(FilePickerSaveOptions options, Func<Stream, Task> writeAsync);

    /// <summary>
    /// Lets the user choose a file and reads it. Returns its content, or <see langword="null"/> when they cancel.
    /// </summary>
    Task<byte[]?> OpenFileAsync(FilePickerOpenOptions options);

    Task<IStorageFile?> TryGetFileFromPathAsync(string absolutePath);
}
