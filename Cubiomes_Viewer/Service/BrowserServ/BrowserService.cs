using Cubiomes_Viewer.Service.TopLevelServ;

namespace Cubiomes_Viewer.Service.BrowserServ;

public sealed class BrowserService : IBrowserService
{
    private readonly ITopLevelProvider _topLevelProvider;

    public BrowserService(ITopLevelProvider topLevelProvider)
    {
        _topLevelProvider = topLevelProvider;
    }

    public async Task OpenAsync(Uri page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!await _topLevelProvider.GetTopLevel().Launcher.LaunchUriAsync(page))
            throw new InvalidOperationException($"No browser opened {page}.");
    }
}
