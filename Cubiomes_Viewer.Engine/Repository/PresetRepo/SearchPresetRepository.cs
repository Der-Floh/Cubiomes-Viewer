using System.Text.Json;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Repository.PresetRepo;

public sealed class SearchPresetRepository : ISearchPresetRepository
{
    public const string StorageKey = "search-presets.json";

    private readonly IAppDataStore _store;
    private readonly ILogger<SearchPresetRepository> _logger;

    public SearchPresetRepository(IAppDataStore store, ILogger<SearchPresetRepository> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchPreset>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var content = await _store.ReadAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        if (content is null)
            return [];

        try
        {
            return JsonSerializer.Deserialize(content, PresetJsonContext.Default.SearchPresetList)?.Presets ?? [];
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "The stored search presets are unreadable; starting without them");
            return [];
        }
    }

    public Task SaveAsync(IReadOnlyList<SearchPreset> presets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presets);

        var content = JsonSerializer.SerializeToUtf8Bytes(new SearchPresetList { Presets = presets }, PresetJsonContext.Default.SearchPresetList);
        return _store.WriteAsync(StorageKey, content, cancellationToken);
    }
}
