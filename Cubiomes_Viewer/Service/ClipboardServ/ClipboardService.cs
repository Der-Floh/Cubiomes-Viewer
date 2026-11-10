using Avalonia.Input.Platform;

using Cubiomes_Viewer.Service.TopLevelServ;

namespace Cubiomes_Viewer.Service.ClipboardServ;

public sealed class ClipboardService : IClipboardService
{
    private readonly ITopLevelProvider _topLevelProvider;

    public ClipboardService(ITopLevelProvider topLevelProvider)
    {
        _topLevelProvider = topLevelProvider;
    }

    public Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var clipboard = _topLevelProvider.GetTopLevel().Clipboard
            ?? throw new InvalidOperationException("The clipboard is not available on this platform.");
        return clipboard.SetTextAsync(text);
    }
}
