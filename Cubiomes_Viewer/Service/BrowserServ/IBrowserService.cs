namespace Cubiomes_Viewer.Service.BrowserServ;

public interface IBrowserService
{
    /// <summary>Opens a web page in the system's browser.</summary>
    /// <exception cref="InvalidOperationException">No browser opened the page.</exception>
    Task OpenAsync(Uri page);
}
