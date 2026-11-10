using Avalonia.Platform.Storage;

namespace Cubiomes_Viewer.Service.StorageServ;

public interface IStorageService
{
    IStorageProvider StorageProvider { get; }
    bool CanOpenFiles { get; }
    bool CanOpenFolders { get; }
    Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options);
    Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options);
    Task<IStorageFile?> TryGetFileFromPathAsync(string absolutePath);
}
