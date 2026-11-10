using System.Text.Json;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Repository.SettingsRepo;

public sealed class SettingsRepository : ISettingsRepository
{
    public const string StorageKey = "settings.json";

    private readonly IAppDataStore _store;
    private readonly ILogger<SettingsRepository> _logger;

    public SettingsRepository(IAppDataStore store, ILogger<SettingsRepository> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var content = await _store.ReadAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        if (content is null)
            return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize(content, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The stored settings are unreadable; using defaults");
            return new AppSettings();
        }
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var content = JsonSerializer.SerializeToUtf8Bytes(settings, SettingsJsonContext.Default.AppSettings);
        return _store.WriteAsync(StorageKey, content, cancellationToken);
    }
}
