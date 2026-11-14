using System.Runtime.InteropServices.JavaScript;

namespace Cubiomes_Viewer.Browser.Interop;

internal static partial class PageInterop
{
    public const string ModuleName = "cv-page";
    public const string ModuleUrl = "../cv-page.js";

    /// <summary>Shows an address in the address bar in place of the current one, without a new entry in the browser's history.</summary>
    [JSImport("replaceAddress", ModuleName)]
    internal static partial void ReplaceAddress(string address);
}
