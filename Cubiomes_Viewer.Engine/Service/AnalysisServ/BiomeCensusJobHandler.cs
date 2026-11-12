using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Service.AnalysisServ;

/// <summary>Counts the biomes of a band of rows of an area's sample grid; samples without a biome are left out.</summary>
public sealed class BiomeCensusJobHandler : IJobHandler<BiomeCensusJob, BiomeCensus>
{
    private const int BiomeIds = 256;

    public BiomeCensus Handle(BiomeCensusJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        var request = job.Request;
        var blocks = request.Area.ToBlocks(request.Reference);
        var grid = SampleGrid.Create(blocks, request.Scale, request.Y);
        ArgumentOutOfRangeException.ThrowIfNegative(job.FirstRow);
        ArgumentOutOfRangeException.ThrowIfLessThan(job.RowCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(job.FirstRow + job.RowCount, grid.SizeZ);
        cancellationToken.ThrowIfCancellationRequested();

        var band = grid with { Z = grid.Z + job.FirstRow, SizeZ = job.RowCount };
        var samples = new byte[band.SampleCount];
        context.GetGenerator(request.World).GenerateBiomes(band, samples);
        return Tally(request, blocks, band, samples);
    }

    /// <summary>Counts each biome and keeps its first sample at the least distance from the reference point.</summary>
    private static BiomeCensus Tally(BiomeCensusRequest request, BlockArea blocks, BiomeArea band, ReadOnlySpan<byte> samples)
    {
        var counts = new int[BiomeIds];
        var nearest = new BlockPos[BiomeIds];
        var distances = new long[BiomeIds];
        var inside = 0;

        for (var index = 0; index < samples.Length; index++)
        {
            var biome = samples[index];
            var position = SampleGrid.GetPosition(band, index, blocks);
            if (biome == Constants.NoBiomeTileValue || !request.Area.Contains(request.Reference, position))
                continue;

            inside++;
            var distance = position.SquaredDistanceTo(request.Reference);
            if (counts[biome]++ == 0 || distance < distances[biome])
                (nearest[biome], distances[biome]) = (position, distance);
        }

        BiomeTally[] tallies = [.. Enumerable.Range(0, BiomeIds).Where(biome => counts[biome] > 0).Select(biome => new BiomeTally(biome, counts[biome], nearest[biome], distances[biome]))];
        return new BiomeCensus { Samples = inside, Tallies = tallies };
    }
}
