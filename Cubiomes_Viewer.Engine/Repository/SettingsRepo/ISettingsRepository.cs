using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Engine.Repository.SettingsRepo;

public interface ISettingsRepository
{
    /// <summary>Loads the stored settings; missing settings yield the defaults.</summary>
    /// <exception cref="UnreadableDataException">The stored settings cannot be read, or a newer version wrote them.</exception>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
