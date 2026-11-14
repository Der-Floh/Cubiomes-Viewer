using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Locate;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Engine.Service.LocateServ;

/// <summary>
/// Splits the range into aligned squares, nearest first, and searches them in waves as wide as there are workers. Structures come from
/// the structure list of the analysis, a square of several regions at a time, and biomes from the biome census, one map tile at 1:4 at a time.
/// </summary>
public sealed class LocateService : ILocateService
{
    private const int DefaultBiomeRange = 6_400;
    private const int StructureRegionsOut = 100;
    private const int PiecesAcrossRange = 12;
    private const int BiomeScale = 4;
    private static readonly JobPriority _priority = new(JobPriorityClass.Interactive);

    private readonly IComputeService _computeService;
    private readonly IStructureFinder _structureFinder;
    private readonly IWorldLandmarkService _landmarkService;

    public LocateService(IComputeService computeService, IStructureFinder structureFinder, IWorldLandmarkService landmarkService)
    {
        _computeService = computeService;
        _structureFinder = structureFinder;
        _landmarkService = landmarkService;
    }

    public int BiomeRange => DefaultBiomeRange;

    public int GetStructureRange(StructureType type, McVersion version)
        => _structureFinder.TryGetPlacement(type, version, out var placement) ? Math.Max(StructureRegionsOut * placement.RegionBlocks, DefaultBiomeRange) : DefaultBiomeRange;

    public Task<LocateResult?> FindStructureAsync(WorldKey world, StructureType type, BlockPos origin, int range, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range);
        if (!_structureFinder.TryGetPlacement(type, world.Version, out var placement))
            return Task.FromResult<LocateResult?>(null);

        var regionsPerPiece = Math.Max(1, (int)Math.Ceiling(range / (double)(PiecesAcrossRange * placement.RegionBlocks)));
        var pieces = Piece.Around(origin, regionsPerPiece * placement.RegionBlocks, range);
        return FindNearestAsync(
            pieces,
            piece => new StructureListJob(new StructureListRequest(world, type, piece.ToSearchArea(origin), origin, Describe: false)),
            listing => listing.Entries.Length > 0 ? listing.Entries[0].Position : null,
            origin, range, progress, cancellationToken);
    }

    public Task<LocateResult?> FindBiomeAsync(WorldKey world, int biome, int y, BlockPos origin, int range, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range);

        var pieces = Piece.Around(origin, TileMetrics.For(world).TileSamples * BiomeScale, range);
        return FindNearestAsync(
            pieces,
            piece => CreateCensusJob(world, piece, origin, y),
            census => census.Tallies.FirstOrDefault(tally => tally.Biome == biome) is { Samples: > 0 } tally ? tally.Nearest : null,
            origin, range, progress, cancellationToken);
    }

    public async Task<LocateResult?> FindStrongholdAsync(WorldKey world, BlockPos origin, CancellationToken cancellationToken = default)
    {
        var overworld = world with { Dimension = Dimension.Overworld };
        var landmarks = _landmarkService.TryGetLandmarks(overworld, out var cached)
            ? cached
            : await _computeService.RunAsync(new LandmarksJob(overworld), _priority, cancellationToken);

        return landmarks.Strongholds
            .Select((position, index) => new LocateResult(position, position.SquaredDistanceTo(origin), index))
            .Aggregate((LocateResult?)null, Nearer);
    }

    /// <summary>Searches the squares nearest first until the nearest place found is nearer than the next square can hold anything.</summary>
    private async Task<LocateResult?> FindNearestAsync<TResult>(List<Piece> pieces, Func<Piece, ComputeJob<TResult>> createJob, Func<TResult, BlockPos?> findNearest,
        BlockPos origin, int range, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var waveSize = Math.Max(1, _computeService.WorkerCount);
        LocateResult? best = null;
        var next = 0;
        while (next < pieces.Count && (best is null || best.SquaredDistance >= pieces[next].MinSquaredDistance))
        {
            var wave = pieces.Skip(next).Take(waveSize).TakeWhile(piece => best is null || piece.MinSquaredDistance <= best.SquaredDistance).ToList();
            ComputeJob<TResult>[] jobs = [.. wave.Select(createJob)];
            foreach (var result in await _computeService.RunAllAsync(jobs, _priority, waveSize, cancellationToken: cancellationToken))
            {
                if (findNearest(result) is { } position)
                    best = Nearer(best, new LocateResult(position, position.SquaredDistanceTo(origin)));
            }

            next += wave.Count;
            progress?.Report(next < pieces.Count ? (int)Math.Min(range, Math.Sqrt(pieces[next].MinSquaredDistance)) : range);
        }

        return best is not null && best.SquaredDistance <= (long)range * range ? best : null;
    }

    private static BiomeCensusJob CreateCensusJob(WorldKey world, Piece piece, BlockPos origin, int y)
        => new(new BiomeCensusRequest(world, piece.ToSearchArea(origin), origin, BiomeScale, y), 0, SampleGrid.Create(piece.ToBlocks(), BiomeScale, y).SizeZ);

    private static LocateResult? Nearer(LocateResult? best, LocateResult candidate)
        => best is null || Compare(candidate, best) < 0 ? candidate : best;

    /// <summary>Orders places by distance, then by Z and X, as the structure list of the analysis does.</summary>
    private static int Compare(LocateResult first, LocateResult second)
    {
        var byDistance = first.SquaredDistance.CompareTo(second.SquaredDistance);
        if (byDistance != 0)
            return byDistance;

        var byZ = first.Position.Z.CompareTo(second.Position.Z);
        return byZ != 0 ? byZ : first.Position.X.CompareTo(second.Position.X);
    }

    /// <summary>A square of blocks that one job searches, aligned to its size, with the least squared distance from the origin to a block in it.</summary>
    private readonly record struct Piece(int X, int Z, int Size, long MinSquaredDistance)
    {
        public BlockArea ToBlocks() => new(X, Z, Size, Size);

        public SearchArea ToSearchArea(BlockPos origin) => SearchArea.Rectangle(X - origin.X, Z - origin.Z, X + Size - 1 - origin.X, Z + Size - 1 - origin.Z);

        /// <summary>The squares of a grid of <paramref name="size"/> blocks that reach within <paramref name="range"/> of the origin inside the world border, nearest first.</summary>
        public static List<Piece> Around(BlockPos origin, int size, int range)
        {
            var (firstColumn, lastColumn) = GetIndices(origin.X, size, range);
            var (firstRow, lastRow) = GetIndices(origin.Z, size, range);
            var rangeSquared = (long)range * range;

            List<Piece> pieces = [];
            for (var row = firstRow; row <= lastRow; row++)
            {
                for (var column = firstColumn; column <= lastColumn; column++)
                {
                    var (x, z) = (column * size, row * size);
                    var distance = GetSquaredDistance(origin, x, z, size);
                    if (distance <= rangeSquared)
                        pieces.Add(new Piece(x, z, size, distance));
                }
            }

            pieces.Sort((first, second) => (first.MinSquaredDistance, first.Z, first.X).CompareTo((second.MinSquaredDistance, second.Z, second.X)));
            return pieces;
        }

        private static (int First, int Last) GetIndices(int origin, int size, int range)
            => (FloorDivide(Math.Max(origin - range, -TilePyramid.WorldLimitBlocks), size), FloorDivide(Math.Min(origin + range, TilePyramid.WorldLimitBlocks), size));

        private static long GetSquaredDistance(BlockPos origin, int x, int z, int size)
        {
            var dx = Math.Max(Math.Max(x - (long)origin.X, 0), origin.X - ((long)x + size - 1));
            var dz = Math.Max(Math.Max(z - (long)origin.Z, 0), origin.Z - ((long)z + size - 1));
            return dx * dx + dz * dz;
        }

        private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
    }
}
