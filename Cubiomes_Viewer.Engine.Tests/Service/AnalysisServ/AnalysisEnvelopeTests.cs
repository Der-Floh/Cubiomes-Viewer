using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Tests.Data.Search;

namespace Cubiomes_Viewer.Engine.Tests.Service.AnalysisServ;

/// <summary>The analysis jobs and their results cross to a Web Worker as JSON, so each one has to come back as it went.</summary>
public sealed class AnalysisEnvelopeTests
{
    private static readonly WorldKey _world = new(-5, McVersion.V1_21_3, Dimension.Nether, GeneratorFlags.LargeBiomes | GeneratorFlags.ForceOceanVariants);

    [Fact]
    public void Jobs_RoundTripThroughTheEnvelope()
    {
        ComputeJob[] jobs =
        [
            new BiomeCensusJob(new BiomeCensusRequest(_world, SearchArea.Circle(300), new BlockPos(5, -9), 16, 70), 3, 12),
            new StructureListJob(new StructureListRequest(_world, StructureType.Bastion, SearchArea.Rectangle(-10, -20, 30, 40), new BlockPos(1, 2), Describe: true)),
            new QuadListJob(new QuadListRequest(_world, StructureType.Igloo, SearchArea.Square(999), new BlockPos(0, 0), QuadQuality.Classic)),
            new ConditionLocationsJob(SampleDefinitions.CreateEveryKindOfCondition(), long.MinValue),
        ];

        Assert.All(jobs, job => Assert.Equal(job, ComputeEnvelope.DeserializeJob(ComputeEnvelope.SerializeJob(job))));
    }

    [Fact]
    public void BiomeCensus_RoundTripsThroughTheEnvelope()
    {
        var census = new BiomeCensus { Samples = 9, Tallies = [new BiomeTally(4, 6, new BlockPos(1, -1), 2), new BiomeTally(170, 3, new BlockPos(-30_000_000, 5), long.MaxValue / 3)] };

        var restored = RoundTrip(census);

        Assert.Equal(census.Samples, restored.Samples);
        Assert.Equal(census.Tallies, restored.Tallies);
    }

    [Fact]
    public void StructureAndQuadListings_RoundTripThroughTheEnvelope()
    {
        var details = new StructureDetails { Type = StructureType.Village, Position = new BlockPos(16, 32), Variant = new StructureVariant { IsAbandoned = true, Size = 3, Biome = 1, Rotation = 2 }, Pieces = [] };
        var structures = new StructureListing { Type = StructureType.Village, Entries = [new StructureEntry(new BlockPos(16, 32), details), new StructureEntry(new BlockPos(-8, 4), null)] };
        var quads = new QuadListing { Type = StructureType.SwampHut, Entries = [new QuadEntry(QuadQuality.Ideal, new BlockPos(1, 2), [new BlockPos(0, 0), new BlockPos(1, 0), new BlockPos(0, 1), new BlockPos(1, 1)], 3)] };

        var restoredStructures = RoundTrip(structures);
        var restoredQuads = RoundTrip(quads);

        Assert.Equal(structures.Entries.Select(entry => entry.Position), restoredStructures.Entries.Select(entry => entry.Position));
        Assert.Equal(details.Variant, restoredStructures.Entries[0].Details?.Variant);
        Assert.Null(restoredStructures.Entries[1].Details);
        var quad = Assert.Single(restoredQuads.Entries);
        Assert.Equal((QuadQuality.Ideal, new BlockPos(1, 2), 3), (quad.Quality, quad.Middle, quad.Generating));
        Assert.Equal(quads.Entries[0].Structures, quad.Structures);
    }

    [Fact]
    public void ConditionLocations_RoundTripThroughTheEnvelope()
    {
        var locations = new ConditionLocations { Seed = long.MaxValue, IsMatch = true, Places = [new ConditionPlace(1, new BlockPos(3, 4)), new ConditionPlace(7, new BlockPos(-5, 6))] };

        var restored = RoundTrip(locations);

        Assert.Equal((locations.Seed, locations.IsMatch), (restored.Seed, restored.IsMatch));
        Assert.Equal(locations.Places, restored.Places);
    }

    private static T RoundTrip<T>(T result) where T : notnull => ComputeEnvelope.DeserializeResponse<T>(ComputeEnvelope.SerializeSuccess(result));
}
