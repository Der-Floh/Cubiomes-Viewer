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

    /// <summary>Lets the user choose where to save a file; returns <see langword="null"/> when they cancel.</summary>
    Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options);

    Task<IStorageFile?> TryGetFileFromPathAsync(string absolutePath);
}
