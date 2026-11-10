using System.Runtime.InteropServices.JavaScript;

using Cubiomes_Viewer.Browser.Interop;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Browser.Service;

/// <summary>Keeps app data in the browser's IndexedDB, where it survives reloads of this site.</summary>
internal sealed class BrowserAppDataStore : IAppDataStore
{
    private Task<JSObject>? _moduleImport;

    public async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await ImportModuleAsync();
            using var record = await StorageInterop.ReadAsync(key);
            return record?.GetPropertyAsByteArray(StorageInterop.ContentProperty);
        }
        catch (JSException ex)
        {
            throw CreateStorageException(ex);
        }
    }

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await ImportModuleAsync();
            await StorageInterop.WriteAsync(key, content.ToArray());
        }
        catch (JSException ex)
        {
            throw CreateStorageException(ex);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await ImportModuleAsync();
            await StorageInterop.DeleteAsync(key);
        }
        catch (JSException ex)
        {
            throw CreateStorageException(ex);
        }
    }

    private Task<JSObject> ImportModuleAsync()
        => _moduleImport ??= JSHost.ImportAsync(StorageInterop.ModuleName, StorageInterop.ModuleUrl);

    private static IOException CreateStorageException(JSException exception)
        => new($"The browser storage is not available: {exception.Message}", exception);
}
