using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Engine.Repository.SettingsRepo;

public sealed class SettingsRepository : ISettingsRepository
{
    public const string StorageKey = "settings.json";

    private const string SchemaVersionKey = "schemaVersion";
    private const string MapKey = "map";
    private const string MarkerSizeKey = "markerSize";

    /// <summary>The schema version from which on Small, Normal and Large draw markers as large as Normal, Large and Very large did.</summary>
    private const int ShiftedMarkerSizesVersion = 2;

    /// <summary>The marker sizes of earlier schemas, which had a fourth, very large size, by the size that now draws as large.</summary>
    private static readonly FrozenDictionary<string, string> _earlierMarkerSizes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Small"] = nameof(MarkerSize.Small),
        ["Normal"] = nameof(MarkerSize.Small),
        ["Large"] = nameof(MarkerSize.Normal),
        ["VeryLarge"] = nameof(MarkerSize.Large),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

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
            settings = JsonSerializer.Deserialize(Upgrade(JsonNode.Parse(content)), SettingsJsonContext.Default.AppSettings);
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

    /// <summary>Brings settings that an earlier schema stored up to the current one; settings without a version count as current.</summary>
    private static JsonNode? Upgrade(JsonNode? root)
    {
        if (root is not JsonObject settings || settings[SchemaVersionKey] is not JsonValue stored || !stored.TryGetValue<int>(out var version)
            || version >= ShiftedMarkerSizesVersion)
            return root;

        if (settings[MapKey] is JsonObject map && map[MarkerSizeKey] is JsonValue size && size.TryGetValue<string>(out var name)
            && _earlierMarkerSizes.TryGetValue(name, out var shifted))
            map[MarkerSizeKey] = shifted;

        settings[SchemaVersionKey] = AppSettings.CurrentSchemaVersion;
        return settings;
    }
}
