using Cubiomes_Viewer.Engine.Data.Settings;

namespace Cubiomes_Viewer.Engine.Service.SettingsServ;

/// <summary>
/// Holds the live settings object and persists it shortly after changes. Nothing is saved before a successful
/// <see cref="LoadAsync"/>, so stored settings that could not be read are never overwritten with defaults.
/// </summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Loads the stored settings; the defaults stay in effect when the storage cannot be read.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Schedules a save; several requests in quick succession result in one write.</summary>
    void RequestSave();

    /// <summary>Writes pending changes immediately.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
