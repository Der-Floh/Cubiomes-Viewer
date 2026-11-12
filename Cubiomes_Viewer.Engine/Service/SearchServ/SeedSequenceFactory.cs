using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

public sealed class SeedSequenceFactory : ISeedSequenceFactory
{
    /// <summary>Region coordinates that keep a quad within the 30 million blocks of a world.</summary>
    public const int MaxRegion = 58_000;

    private static readonly QuadQuality[] _qualities = [QuadQuality.Ideal, QuadQuality.Classic, QuadQuality.Normal, QuadQuality.Barely];

    private readonly IQuadHutBaseService _quadBases;
    private readonly IQuadFinder _quadFinder;
    private readonly IStructureCatalog _structureCatalog;

    public SeedSequenceFactory(IQuadHutBaseService quadBases, IQuadFinder quadFinder, IStructureCatalog structureCatalog)
    {
        _quadBases = quadBases;
        _quadFinder = quadFinder;
        _structureCatalog = structureCatalog;
    }

    public IEnumerable<string> Validate(SeedSource source, McVersion version) => source switch
    {
        SeedRangeSource range => ValidateRange(range),
        QuadHutSeedSource quad => ValidateQuad(quad, version),
        SeedListSource list => ValidateList(list),
        StructureSeedSource structureSeeds => ValidateStructureSeeds(structureSeeds),
        _ => throw new NotSupportedException($"Unsupported seed source: {source?.GetType().Name}"),
    };

    public async Task<ISeedSequence> CreateAsync(SeedSource source, SearchDefinition definition, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return source switch
        {
            SeedRangeSource range => new SeedRangeSequence(range),
            QuadHutSeedSource quad => await CreateQuadSequenceAsync(quad, definition, progress, cancellationToken),
            SeedListSource list => new SeedListSequence(list),
            StructureSeedSource structureSeeds => new StructureSeedSequence(structureSeeds),
            _ => throw new NotSupportedException($"Unsupported seed source: {source?.GetType().Name}"),
        };
    }

    private async Task<ISeedSequence> CreateQuadSequenceAsync(QuadHutSeedSource source, SearchDefinition definition, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var bases = await _quadBases.GetBasesAsync(source.Quality, progress, cancellationToken);
        return new QuadHutSeedSequence([.. bases.Select(value => _quadFinder.GetSeedOfBase(source.Type, definition.Version, definition.Rules, value, source.RegionX, source.RegionZ))]);
    }

    private static IEnumerable<string> ValidateRange(SeedRangeSource range)
    {
        if (range.LastSeed < range.FirstSeed)
            yield return "The last seed comes before the first one.";
    }

    private IEnumerable<string> ValidateQuad(QuadHutSeedSource quad, McVersion version)
    {
        if (!_qualities.Contains(quad.Quality))
            yield return "Choose how close the four huts have to be.";
        if (!Enum.IsDefined(quad.Type) || !_quadFinder.SupportsQuads(quad.Type, version))
            yield return $"{DescribeType(quad.Type)} form no quads in this version.";
        if (Math.Abs(quad.RegionX) > MaxRegion || Math.Abs(quad.RegionZ) > MaxRegion)
            yield return $"The regions of the quad must lie within {MaxRegion} regions of the origin.";
    }

    private static IEnumerable<string> ValidateList(SeedListSource list)
    {
        if (list.Seeds.Count == 0)
            yield return "Add at least one seed to the list.";
        else if (list.Seeds.Count > SeedListSource.MaxSeeds)
            yield return $"A list may hold at most {SeedListSource.MaxSeeds} seeds.";
    }

    private static IEnumerable<string> ValidateStructureSeeds(StructureSeedSource structureSeeds)
    {
        if (structureSeeds.FirstStructureSeed is < 0 or > StructureSeed.Max || structureSeeds.LastStructureSeed is < 0 or > StructureSeed.Max)
            yield return $"A structure seed is a whole number from 0 to {StructureSeed.Max}.";
        else if (structureSeeds.LastStructureSeed < structureSeeds.FirstStructureSeed)
            yield return "The last structure seed comes before the first one.";
    }

    private string DescribeType(StructureType type)
        => _structureCatalog.Structures.FirstOrDefault(info => info.Type == type) is { } info ? $"{info.Name}s" : type.ToString();
}
