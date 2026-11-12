using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Service.AnalysisServ;

public sealed class AnalysisService : IAnalysisService
{
    public const int MaxBiomeSamples = 4096 * 4096;
    public const int MaxRegions = 65_536;

    private const int SamplesPerJob = 65_536;
    private const int JobsPerWorker = 2;
    private static readonly int[] _scales = [1, 4, 16, 64, 256];
    private static readonly JobPriority _priority = new(JobPriorityClass.Background);

    private readonly IComputeService _compute;
    private readonly IStructureFinder _structureFinder;
    private readonly IQuadFinder _quadFinder;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IConditionValidator _conditionValidator;

    public AnalysisService(IComputeService compute, IStructureFinder structureFinder, IQuadFinder quadFinder, IStructureCatalog structureCatalog, IConditionValidator conditionValidator)
    {
        _compute = compute;
        _structureFinder = structureFinder;
        _quadFinder = quadFinder;
        _structureCatalog = structureCatalog;
        _conditionValidator = conditionValidator;
    }

    private int MaxRunning => Math.Max(1, _compute.WorkerCount * JobsPerWorker);

    public IReadOnlyList<string> FindBiomeProblems(BiomeCensusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_scales.Contains(request.Scale))
            return [$"The distance between samples must be one of {string.Join(", ", _scales)} blocks."];

        var grid = CreateGrid(request);
        return (long)grid.SizeX * grid.SizeZ > MaxBiomeSamples
            ? [string.Create(CultureInfo.InvariantCulture, $"The area needs more than {MaxBiomeSamples:N0} samples at {request.Scale} blocks per sample; sample it more coarsely or make it smaller.")]
            : [];
    }

    public IReadOnlyList<string> FindStructureProblems(StructureType type, McVersion version, SearchArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        if (!_structureFinder.TryGetPlacement(type, version, out var placement))
            return [$"{DescribeType(type)} does not generate in this version."];
        return FindRegionProblems(area, placement);
    }

    public async Task<IReadOnlyList<BiomeCensus>> CountBiomesAsync(IReadOnlyList<BiomeCensusRequest> requests, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        foreach (var request in requests)
            ThrowIfInvalid(FindBiomeProblems(request), nameof(requests));

        List<(int Request, BiomeCensusJob Job)> bands = [.. requests.SelectMany((request, index) => SplitIntoBands(request).Select(job => (index, job)))];
        var results = await _compute.RunAllAsync<BiomeCensus>([.. bands.Select(band => band.Job)], _priority, MaxRunning, progress, cancellationToken);

        var census = Enumerable.Repeat(BiomeCensus.Empty, requests.Count).ToArray();
        for (var index = 0; index < bands.Count; index++)
            census[bands[index].Request] = census[bands[index].Request].Add(results[index]);
        return census;
    }

    public async Task<IReadOnlyList<StructureListing>> ListStructuresAsync(IReadOnlyList<StructureListRequest> requests, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        foreach (var request in requests)
            ThrowIfInvalid(FindStructureProblems(request.Type, request.World.Version, request.Area), nameof(requests));

        return await _compute.RunAllAsync<StructureListing>([.. requests.Select(request => new StructureListJob(request))], _priority, MaxRunning, progress, cancellationToken);
    }

    public async Task<IReadOnlyList<QuadListing>> ListQuadsAsync(IReadOnlyList<QuadListRequest> requests, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        foreach (var request in requests)
            ThrowIfInvalid(FindQuadProblems(request.Type, request.World.Version, request.Area), nameof(requests));

        return await _compute.RunAllAsync<QuadListing>([.. requests.Select(request => new QuadListJob(request))], _priority, MaxRunning, progress, cancellationToken);
    }

    public async Task<IReadOnlyList<WorldLandmarks>> FindLandmarksAsync(IReadOnlyList<WorldKey> worlds, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worlds);
        return await _compute.RunAllAsync<WorldLandmarks>([.. worlds.Select(world => new LandmarksJob(world))], _priority, MaxRunning, progress, cancellationToken);
    }

    public async Task<IReadOnlyList<ConditionLocations>> FindConditionLocationsAsync(SearchDefinition definition, IReadOnlyList<long> seeds, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(seeds);

        var problems = _conditionValidator.Validate(definition);
        if (problems.Count > 0)
            throw new InvalidSearchException(problems);

        return await _compute.RunAllAsync<ConditionLocations>([.. seeds.Select(seed => new ConditionLocationsJob(definition, seed))], _priority, MaxRunning, progress, cancellationToken);
    }

    public IReadOnlyList<string> FindQuadProblems(StructureType type, McVersion version, SearchArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        if (!_quadFinder.SupportsQuads(type, version) || !_structureFinder.TryGetPlacement(type, version, out var placement))
            return [$"{DescribeType(type)}s form no quads in this version."];
        return FindRegionProblems(area, placement);
    }

    private static IReadOnlyList<string> FindRegionProblems(SearchArea area, StructurePlacement placement)
    {
        var regions = ((long)area.SizeX / placement.RegionBlocks + 3) * ((long)area.SizeZ / placement.RegionBlocks + 3);
        return regions > MaxRegions
            ? [string.Create(CultureInfo.InvariantCulture, $"The area is too large: it may span at most {MaxRegions:N0} regions of {placement.RegionBlocks} blocks.")]
            : [];
    }

    private static IEnumerable<BiomeCensusJob> SplitIntoBands(BiomeCensusRequest request)
    {
        var grid = CreateGrid(request);
        var rowsPerBand = Math.Max(1, SamplesPerJob / grid.SizeX);
        for (var row = 0; row < grid.SizeZ; row += rowsPerBand)
            yield return new BiomeCensusJob(request, row, Math.Min(rowsPerBand, grid.SizeZ - row));
    }

    private static BiomeArea CreateGrid(BiomeCensusRequest request) => SampleGrid.Create(request.Area.ToBlocks(request.Reference), request.Scale, request.Y);

    private static void ThrowIfInvalid(IReadOnlyList<string> problems, string parameterName)
    {
        if (problems.Count > 0)
            throw new ArgumentException(problems[0], parameterName);
    }

    private string DescribeType(StructureType type) => _structureCatalog.Structures.FirstOrDefault(info => info.Type == type)?.Name ?? type.ToString();
}
