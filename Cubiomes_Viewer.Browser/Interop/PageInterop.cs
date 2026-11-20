using System.Runtime.InteropServices.JavaScript;

namespace Cubiomes_Viewer.Browser.Interop;

internal static partial class PageInterop
{
    public const string ModuleName = "cv-page";
    public static readonly string ModuleUrl = ModuleUrls.For("cv-page.js");

    /// <summary>Shows an address in the address bar in place of the current one, without a new entry in the browser's history.</summary>
    [JSImport("replaceAddress", ModuleName)]
    internal static partial void ReplaceAddress(string address);

    /// <summary>Adds an entry with an address to the browser's history, marked with its step among the entries of the app.</summary>
    [JSImport("pushAddress", ModuleName)]
    internal static partial void PushAddress(string address, int step);

    /// <summary>The step of the history entry the page shows; 0 for the entry the app was opened with.</summary>
    [JSImport("historyStep", ModuleName)]
    internal static partial int HistoryStep();

    [JSImport("goBack", ModuleName)]
    internal static partial void GoBack();

    [JSImport("goForward", ModuleName)]
    internal static partial void GoForward();

    /// <summary>Calls back with the address and step of the entry the browser moved to, after Back or Forward.</summary>
    [JSImport("watchHistory", ModuleName)]
    internal static partial void WatchHistory([JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onMoved);
}
