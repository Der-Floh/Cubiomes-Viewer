using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Browser.Interop;

/// <summary>
/// The addresses of the page's script modules. They carry the app's version, so that a browser holding a module of an older
/// version in its cache loads the new one after an update instead of calling functions the old one lacks.
/// </summary>
internal static class ModuleUrls
{
    public static string For(string file) => $"../{file}?v={Uri.EscapeDataString(AppVersion.Current)}";
}
