using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Service.SettingsServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SettingsServ;

/// <summary>Settings held in memory that count the saves asked for instead of writing them.</summary>
internal sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Current { get; } = new();
    public int SaveRequests { get; private set; }

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void RequestSave() => SaveRequests++;
    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
