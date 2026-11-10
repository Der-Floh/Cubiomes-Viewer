namespace Cubiomes_Viewer.Service.ClipboardServ;

public interface IClipboardService
{
    /// <summary>Puts text on the system clipboard.</summary>
    /// <exception cref="InvalidOperationException">The platform offers no clipboard.</exception>
    Task SetTextAsync(string text);
}
