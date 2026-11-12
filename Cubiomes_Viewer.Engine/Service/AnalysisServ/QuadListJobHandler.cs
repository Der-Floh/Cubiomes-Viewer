using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.AnalysisServ;

/// <summary>Lists the quads of a structure type whose middle lies in an area, with how many of the four the biomes let generate.</summary>
public sealed class QuadListJobHandler(IQuadFinder quadFinder, IStructureFinder structureFinder) : IJobHandler<QuadListJob, QuadListing>
{
    public QuadListing Handle(QuadListJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var request = job.Request;
        var version = request.World.Version;
        if (!quadFinder.SupportsQuads(request.Type, version) || !structureFinder.TryGetPlacement(request.Type, version, out var placement))
            return new QuadListing { Type = request.Type, Entries = [] };

        var quads = FindQuads(request, placement.RegionBlocks);
        var generator = context.GetGenerator(request.World with { Dimension = placement.Dimension });
        List<QuadEntry> entries = [];
        Span<BlockPos> positions = stackalloc BlockPos[StructureFinderExtensions.StructuresPerQuad];

        foreach (var quad in quads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (quad.Quality > request.Worst || !structureFinder.TryGetQuadPositions(request.Type, version, request.World.Seed, quad, positions))
                continue;

            var middle = BlockPos.MiddleOf(positions);
            if (request.Area.Contains(request.Reference, middle))
                entries.Add(new QuadEntry(quad.Quality, middle, positions.ToArray(), CountGenerating(generator, request.Type, positions)));
        }

        return new QuadListing { Type = request.Type, Entries = [.. entries.OrderBy(entry => entry.Quality).ThenBy(entry => entry.Middle.SquaredDistanceTo(request.Reference))] };
    }

    /// <summary>The quads whose first region lies one region before the area up to its end, so that every quad with its middle inside is among them.</summary>
    private ReadOnlySpan<QuadPosition> FindQuads(QuadListRequest request, int regionBlocks)
    {
        var blocks = request.Area.ToBlocks(request.Reference);
        var (x0, z0) = (FloorDivide(blocks.X, regionBlocks) - 1, FloorDivide(blocks.Z, regionBlocks) - 1);
        var (x1, z1) = (FloorDivide(blocks.Right - 1, regionBlocks), FloorDivide(blocks.Bottom - 1, regionBlocks));
        var quads = new QuadPosition[(x1 - x0 + 1) * (z1 - z0 + 1)];
        var count = quadFinder.FindQuads(request.Type, request.World.Version, request.World.Seed, x0, z0, x1, z1, quads);
        return quads.AsSpan(0, count);
    }

    private static int CountGenerating(IWorldGenerator generator, StructureType type, ReadOnlySpan<BlockPos> positions)
    {
        var generating = 0;
        foreach (var position in positions)
            generating += generator.CanGenerateStructure(type, position) ? 1 : 0;
        return generating;
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}
