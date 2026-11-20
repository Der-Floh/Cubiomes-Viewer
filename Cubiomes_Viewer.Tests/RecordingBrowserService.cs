using Cubiomes_Viewer.Service.BrowserServ;

namespace Cubiomes_Viewer.Tests;

internal sealed class RecordingBrowserService : IBrowserService
{
    public List<Uri> Pages { get; } = [];

    public Task OpenAsync(Uri page)
    {
        Pages.Add(page);
        return Task.CompletedTask;
    }
}
