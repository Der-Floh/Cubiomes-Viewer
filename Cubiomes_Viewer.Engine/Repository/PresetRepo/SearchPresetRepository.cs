using System.Text.Json;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Engine.Repository.PresetRepo;

public sealed class SearchPresetRepository : ISearchPresetRepository
{
    public const string StorageKey = "search-presets.json";

    private readonly IAppDataStore _store;

    public SearchPresetRepository(IAppDataStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<SearchPreset>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var content = await _store.ReadAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        return content is null ? [] : Deserialize(content);
    }

    public Task SaveAsync(IReadOnlyList<SearchPreset> presets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presets);

        var content = JsonSerializer.SerializeToUtf8Bytes(new SearchPresetList { Presets = presets }, PresetJsonContext.Default.SearchPresetList);
        return _store.WriteAsync(StorageKey, content, cancellationToken);
    }

    private static IReadOnlyList<SearchPreset> Deserialize(byte[] content)
    {
        SearchPresetList? list;
        try
        {
            list = JsonSerializer.Deserialize(content, PresetJsonContext.Default.SearchPresetList);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new UnreadableDataException(StorageKey, content, "The stored search presets are not presets of Cubiomes Viewer.", ex);
        }

        if (list?.Presets is null)
            throw new UnreadableDataException(StorageKey, content, "The stored search presets are empty.");
        if (list.SchemaVersion > SearchPresetList.CurrentSchemaVersion)
            throw new UnreadableDataException(StorageKey, content, "The stored search presets were written by a newer version of Cubiomes Viewer.");
        return list.Presets;
    }
}
