using System.Text.Json;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Engine.Repository.SettingsRepo;

public sealed class SettingsRepository : ISettingsRepository
{
    public const string StorageKey = "settings.json";

    private readonly IAppDataStore _store;

    public SettingsRepository(IAppDataStore store)
    {
        _store = store;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var content = await _store.ReadAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        return content is null ? new AppSettings() : Deserialize(content);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var content = JsonSerializer.SerializeToUtf8Bytes(settings, SettingsJsonContext.Default.AppSettings);
        return _store.WriteAsync(StorageKey, content, cancellationToken);
    }

    private static AppSettings Deserialize(byte[] content)
    {
        AppSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize(content, SettingsJsonContext.Default.AppSettings);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new UnreadableDataException(StorageKey, content, "The stored settings are not settings of Cubiomes Viewer.", ex);
        }

        if (settings is null)
            throw new UnreadableDataException(StorageKey, content, "The stored settings are empty.");
        if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
            throw new UnreadableDataException(StorageKey, content, "The stored settings were written by a newer version of Cubiomes Viewer.");
        return settings;
    }
}
