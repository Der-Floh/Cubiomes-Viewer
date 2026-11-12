using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.AnalysisServ;

/// <summary>
/// Looks at worlds more closely than the map does: how much of an area each biome covers, which structures and quads
/// lie in it, and where the fixed points and the conditions of a search are. The work runs on the compute workers;
/// results come back in the order of the requests.
/// </summary>
public interface IAnalysisService
{
    /// <summary>What keeps an area from being counted: a sample spacing it does not offer, or too many samples.</summary>
    IReadOnlyList<string> FindBiomeProblems(BiomeCensusRequest request);

    /// <summary>What keeps structures of a type from being listed in an area of a version.</summary>
    IReadOnlyList<string> FindStructureProblems(StructureType type, McVersion version, SearchArea area);

    /// <summary>What keeps quads of a type from being listed in an area of a version.</summary>
    IReadOnlyList<string> FindQuadProblems(StructureType type, McVersion version, SearchArea area);

    /// <summary>Counts the biomes of an area in each world; large areas are split into bands of rows for the workers.</summary>
    Task<IReadOnlyList<BiomeCensus>> CountBiomesAsync(IReadOnlyList<BiomeCensusRequest> requests, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StructureListing>> ListStructuresAsync(IReadOnlyList<StructureListRequest> requests, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuadListing>> ListQuadsAsync(IReadOnlyList<QuadListRequest> requests, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Finds the spawn point and the strongholds of each world.</summary>
    Task<IReadOnlyList<WorldLandmarks>> FindLandmarksAsync(IReadOnlyList<WorldKey> worlds, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Tests each seed against a valid definition and finds where its conditions hold.</summary>
    Task<IReadOnlyList<ConditionLocations>> FindConditionLocationsAsync(SearchDefinition definition, IReadOnlyList<long> seeds, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
