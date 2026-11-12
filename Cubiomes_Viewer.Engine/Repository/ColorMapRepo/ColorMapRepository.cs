using System.Text.Json;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Repository.ColorMapRepo;

public sealed class ColorMapRepository : IColorMapRepository
{
    public const string StorageKey = "color-maps.json";

    private readonly IAppDataStore _store;
    private readonly ILogger<ColorMapRepository> _logger;

    public ColorMapRepository(IAppDataStore store, ILogger<ColorMapRepository> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BiomeColorMap>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var content = await _store.ReadAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        if (content is null)
            return [];

        try
        {
            return Deserialize(content);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "The stored colour maps are unreadable; starting without them");
            return [];
        }
    }

    public Task SaveAsync(IReadOnlyList<BiomeColorMap> maps, CancellationToken cancellationToken = default)
        => _store.WriteAsync(StorageKey, Serialize(maps), cancellationToken);

    public byte[] Serialize(IReadOnlyList<BiomeColorMap> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return JsonSerializer.SerializeToUtf8Bytes(new BiomeColorMapFile { Maps = [.. maps] }, ColorMapJsonContext.Default.BiomeColorMapFile);
    }

    public IReadOnlyList<BiomeColorMap> Deserialize(ReadOnlySpan<byte> content)
    {
        BiomeColorMapFile? file;
        try
        {
            file = JsonSerializer.Deserialize(content, ColorMapJsonContext.Default.BiomeColorMapFile);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"This is no colour map file: {ex.Message}", ex);
        }

        if (file is null || file.Maps is null)
            throw new FormatException("This is no colour map file.");
        if (file.SchemaVersion > BiomeColorMapFile.CurrentSchemaVersion)
            throw new FormatException("The colour maps were saved by a newer version of Cubiomes Viewer.");

        foreach (var map in file.Maps)
            Validate(map);
        return file.Maps;
    }

    private static void Validate(BiomeColorMap? map)
    {
        if (map is null || string.IsNullOrWhiteSpace(map.Name) || map.Name.Length > BiomeColorMap.MaxNameLength)
            throw new FormatException($"Every colour map needs a name of at most {BiomeColorMap.MaxNameLength} characters.");
        if (map.Colors.Any(color => color.Biome is < 0 or >= Constants.NoBiomeTileValue))
            throw new FormatException($"The colour map '{map.Name}' names a biome id outside 0 to {Constants.NoBiomeTileValue - 1}.");
    }
}
