using Cubiomes_Viewer.Engine.Data.Settings;

namespace Cubiomes_Viewer.Engine.Service.SettingsServ;

/// <summary>Holds the live settings object and persists it shortly after changes.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Schedules a save; several requests in quick succession result in one write.</summary>
    void RequestSave();

    /// <summary>Writes pending changes immediately.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
