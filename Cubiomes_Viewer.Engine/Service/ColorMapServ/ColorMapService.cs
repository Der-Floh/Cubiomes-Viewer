using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Repository.ColorMapRepo;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ColorMapServ;

public sealed class ColorMapService : IColorMapService
{
    private readonly IColorMapRepository _repository;
    private readonly IBiomeCatalog _biomeCatalog;
    private List<BiomeColorMap> _maps = [];

    public ColorMapService(IColorMapRepository repository, IBiomeCatalog biomeCatalog)
    {
        _repository = repository;
        _biomeCatalog = biomeCatalog;
    }

    public IReadOnlyList<BiomeColorMap> Maps => _maps;

    public BiomePalette DefaultPalette => _biomeCatalog.DefaultPalette;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
        => _maps = Sort(await _repository.LoadAsync(cancellationToken));

    public Task SaveAsync(BiomeColorMap map, string? previousName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (string.IsNullOrWhiteSpace(map.Name) || map.Name.Length > BiomeColorMap.MaxNameLength)
            throw new ArgumentException($"A colour map needs a name of at most {BiomeColorMap.MaxNameLength} characters.", nameof(map));

        _maps = Sort([.. _maps.Where(stored => !IsNamed(stored, map.Name) && !IsNamed(stored, previousName)), map]);
        return _repository.SaveAsync(_maps, cancellationToken);
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        _maps = [.. _maps.Where(stored => !IsNamed(stored, name))];
        return _repository.SaveAsync(_maps, cancellationToken);
    }

    public BiomePalette GetPalette(BiomeColorMap? map) => map is null ? DefaultPalette : DefaultPalette.With(map.Colors);

    public async Task<IReadOnlyList<BiomeColorMap>> ImportAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        List<BiomeColorMap> imported = [];
        foreach (var map in _repository.Deserialize(content.Span))
        {
            var stored = map with { Name = GetFreeName(map.Name) };
            _maps = Sort([.. _maps, stored]);
            imported.Add(stored);
        }

        await _repository.SaveAsync(_maps, cancellationToken);
        return imported;
    }

    public byte[] Export(BiomeColorMap map) => _repository.Serialize([map]);

    public string GetFreeName(string wanted)
    {
        var name = wanted.Trim();
        if (name.Length > BiomeColorMap.MaxNameLength)
            name = name[..BiomeColorMap.MaxNameLength];

        for (var number = 2; _maps.Any(stored => IsNamed(stored, name)); number++)
            name = string.Create(CultureInfo.InvariantCulture, $"{wanted.Trim()} {number}");
        return name;
    }

    private static bool IsNamed(BiomeColorMap map, string? name) => string.Equals(map.Name, name, StringComparison.OrdinalIgnoreCase);

    private static List<BiomeColorMap> Sort(IEnumerable<BiomeColorMap> maps) => [.. maps.OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase)];
}
