using Cubiomes_Viewer.Engine.Data.Settings;

namespace Cubiomes_Viewer.Engine.Repository.SettingsRepo;

public interface ISettingsRepository
{
    /// <summary>Loads the stored settings; missing or unreadable settings yield the defaults.</summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
