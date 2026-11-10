using Cubiomes_Viewer.Service.ClipboardServ;

namespace Cubiomes_Viewer.Tests;

internal sealed class RecordingClipboardService : IClipboardService
{
    public List<string> Texts { get; } = [];

    public Task SetTextAsync(string text)
    {
        Texts.Add(text);
        return Task.CompletedTask;
    }
}
