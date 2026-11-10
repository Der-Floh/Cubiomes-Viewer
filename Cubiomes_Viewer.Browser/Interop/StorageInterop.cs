using System.Runtime.InteropServices.JavaScript;

namespace Cubiomes_Viewer.Browser.Interop;

internal static partial class StorageInterop
{
    public const string ModuleName = "cv-storage";
    public const string ModuleUrl = "../cv-storage.js";
    public const string ContentProperty = "content";

    /// <summary>
    /// Returns the stored record, which holds its bytes in <see cref="ContentProperty"/>,
    /// or <see langword="null"/> when nothing is stored under <paramref name="key"/>.
    /// </summary>
    [JSImport("read", ModuleName)]
    internal static partial Task<JSObject?> ReadAsync(string key);

    [JSImport("write", ModuleName)]
    internal static partial Task WriteAsync(string key, byte[] content);

    [JSImport("remove", ModuleName)]
    internal static partial Task DeleteAsync(string key);
}
