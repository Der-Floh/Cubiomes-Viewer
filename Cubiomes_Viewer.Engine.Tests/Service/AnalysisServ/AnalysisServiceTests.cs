using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.AnalysisServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.AnalysisServ;

/// <summary>The analysis on the real workers, against counting and finding everything directly with one generator.</summary>
public sealed class AnalysisServiceTests : IDisposable
{
    private const ulong IdealLowBits = 0x43f18;
    private const int AroundTheOrigin = -1;
    private static readonly BlockPos _reference = new(-700, 1300);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    private readonly IAnalysisService _analysis;
    private readonly ConditionTestBench _bench = new();

    public AnalysisServiceTests()
    {
        _services = new ServiceCollection().AddLogging().AddCubiomesEngine().AddSingleton<IAppDataStore>(new FileSystemAppDataStore(_directory)).BuildServiceProvider();
        _analysis = _services.GetRequiredService<IAnalysisService>();
    }

    public void Dispose()
    {
        _services.Dispose();
        _bench.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    public static TheoryData<McVersion, long, int, int, bool> BiomeAreas() => new()
    {
        { McVersion.V1_16_5, 42, 4, 2048, false },
        { McVersion.V1_16_5, -7, 16, 6000, true },
        { McVersion.V1_12_2, 1234, 64, 20_000, false },
        { McVersion.V1_21_3, 99, 16, 4096, true },
    };

    [Theory]
    [MemberData(nameof(BiomeAreas))]
    public async Task CountBiomesAsync_EqualsCountingEverySampleOfTheArea(McVersion version, long seed, int scale, int side, bool circle)
    {
        var area = circle ? SearchArea.Circle(side / 2) : SearchArea.Square(side);
        var world = new WorldKey(seed, version, Dimension.Overworld, new SearchDefinition().Flags);
        var request = new BiomeCensusRequest(world, area, _reference, scale, ViewSettings.DefaultSampleHeight);
        var expected = CountDirectly(request);

        var census = Assert.Single(await _analysis.CountBiomesAsync([request], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(expected.Samples, census.Samples);
        Assert.Equal(expected.Tallies, census.Tallies);
        Assert.True(census.Tallies.Length > 2);
    }

    [Fact]
    public async Task CountBiomesAsync_SeveralWorlds_ComeBackInTheOrderOfTheRequests()
    {
        BiomeCensusRequest[] requests = [.. Enumerable.Range(0, 6).Select(index => new BiomeCensusRequest(new WorldKey(index * 1000L, McVersion.V1_16_5, Dimension.Overworld, new SearchDefinition().Flags), SearchArea.Square(3000), _reference, 16, 64))];
        List<double> progress = [];

        var census = await _analysis.CountBiomesAsync(requests, new Progress<double>(progress.Add), TestContext.Current.CancellationToken);

        Assert.Equal(requests.Select(CountDirectly).Select(expected => expected.Tallies), census.Select(result => result.Tallies));
    }

    [Theory]
    [InlineData(StructureType.Village, McVersion.V1_16_5, 5000)]
    [InlineData(StructureType.Fortress, McVersion.V1_16_5, 3000)]
    [InlineData(StructureType.EndCity, McVersion.V1_21_3, 4000)]
    [InlineData(StructureType.RuinedPortal, McVersion.V1_21_3, 2500)]
    public async Task ListStructuresAsync_EqualsTheStructuresTheGeneratorFinds(StructureType type, McVersion version, int side)
    {
        var finder = new CubiomesStructureFinder();
        Assert.True(finder.TryGetPlacement(type, version, out var placement));
        var world = new WorldKey(77, version, Dimension.Overworld, new SearchDefinition().Flags);
        var area = SearchArea.Circle(side);
        var generator = _bench.Open(new SearchDefinition { Version = version }, world.Seed).GetGenerator(placement.Dimension);
        var expected = generator.FindStructures(type, area.ToBlocks(_reference)).Where(position => area.Contains(_reference, position)).OrderBy(position => position.SquaredDistanceTo(_reference)).ToList();

        var listing = Assert.Single(await _analysis.ListStructuresAsync([new StructureListRequest(world, type, area, _reference, Describe: true)], cancellationToken: TestContext.Current.CancellationToken));

        Assert.NotEmpty(expected);
        Assert.Equal(expected, listing.Entries.Select(entry => entry.Position));
        Assert.All(listing.Entries, entry =>
        {
            var details = generator.DescribeStructure(type, entry.Position);
            Assert.NotNull(entry.Details);
            Assert.Equal(details.Variant, entry.Details.Variant);
            Assert.Equal(details.Pieces.Length, entry.Details.Pieces.Length);
        });
    }

    [Fact]
    public async Task ListQuadsAsync_QuadAroundTheOrigin_IsListedWithItsFourHuts()
    {
        var quadFinder = new CubiomesQuadFinder();
        var structureFinder = new CubiomesStructureFinder();
        var bases = new ulong[4096];
        var count = quadFinder.ScanQuadHutBases(IdealLowBits, 0, 1 << 22, bases);
        var lower = quadFinder.GetSeedOfBase(StructureType.SwampHut, McVersion.V1_16_5, StructureRules.Default, bases[0], AroundTheOrigin, AroundTheOrigin);
        var world = new WorldKey(lower, McVersion.V1_16_5, Dimension.Overworld, new SearchDefinition().Flags);
        BlockPos[] huts = [.. new[] { (-1, -1), (0, -1), (-1, 0), (0, 0) }.Select(region => structureFinder.TryGetRegionPosition(StructureType.SwampHut, McVersion.V1_16_5, StructureRules.Default, lower, region.Item1, region.Item2, out var hut) ? hut : default)];
        var generator = _bench.Open(new SearchDefinition { Version = McVersion.V1_16_5 }, lower).GetGenerator(Dimension.Overworld);

        var listing = Assert.Single(await _analysis.ListQuadsAsync([new QuadListRequest(world, StructureType.SwampHut, SearchArea.Square(2048), new BlockPos(0, 0))], cancellationToken: TestContext.Current.CancellationToken));

        Assert.True(count > 0);
        var quad = listing.Entries[0];
        Assert.Equal(QuadQuality.Ideal, quad.Quality);
        Assert.Equal(huts, quad.Structures);
        Assert.Equal(BlockPos.MiddleOf(huts), quad.Middle);
        Assert.Equal(huts.Count(hut => generator.CanGenerateStructure(StructureType.SwampHut, hut)), quad.Generating);
    }

    [Fact]
    public async Task FindLandmarksAsync_EqualsTheSpawnAndStrongholdsOfTheGenerator()
    {
        var world = new WorldKey(8_675_309, McVersion.V1_16_5, Dimension.Overworld, new SearchDefinition().Flags);
        var generator = _bench.Open(new SearchDefinition { Version = world.Version }, world.Seed).GetGenerator(Dimension.Overworld);

        var landmarks = Assert.Single(await _analysis.FindLandmarksAsync([world], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(generator.FindSpawn(), landmarks.Spawn);
        Assert.Equal(generator.FindStrongholds(), landmarks.Strongholds);
    }

    [Fact]
    public async Task FindConditionLocationsAsync_ListsWhereEachConditionWasFound()
    {
        var definition = ConditionTestBench.Define(McVersion.V1_16_5,
            new StructureCondition { Id = 1, Type = StructureType.Village, Area = SearchArea.Square(2048) },
            new StructureCondition { Id = 2, RelativeTo = 1, Type = StructureType.RuinedPortal, Area = SearchArea.Square(600) });
        var plan = TestEngine.CreateSearchPlanCompiler().Compile(definition);
        var matcher = new SeedMatcher();
        var matching = Enumerable.Range(500, 60).Select(seed => (long)seed).First(seed => matcher.IsMatch(plan, _bench.Open(definition, seed)));
        var failing = Enumerable.Range(500, 60).Select(seed => (long)seed).First(seed => !matcher.IsMatch(plan, _bench.Open(definition, seed)));
        var generator = _bench.Open(definition, matching).GetGenerator(Dimension.Overworld);
        var villages = generator.FindStructures(StructureType.Village, definition.Conditions[0].Area.ToBlocks(new BlockPos(0, 0)));

        var locations = await _analysis.FindConditionLocationsAsync(definition, [matching, failing], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([(matching, true), (failing, false)], locations.Select(location => (location.Seed, location.IsMatch)));
        var places = locations[0].Places;
        Assert.Equal(villages, places.Where(place => place.ConditionId == 1).Select(place => place.Position));
        var portals = places.Where(place => place.ConditionId == 2).Select(place => place.Position).ToList();
        Assert.NotEmpty(portals);
        Assert.Contains(villages, village => portals.All(portal => definition.Conditions[1].Area.Contains(village, portal)));
    }

    [Fact]
    public void FindProblems_AreasThatCannotBeAnalysed_SayWhy()
    {
        var world = new WorldKey(1, McVersion.V1_16_5, Dimension.Overworld, new SearchDefinition().Flags);

        Assert.Equal(["The distance between samples must be one of 1, 4, 16, 64, 256 blocks."], _analysis.FindBiomeProblems(new BiomeCensusRequest(world, SearchArea.Square(100), _reference, 8, 64)));
        Assert.Equal(["The area needs more than 16,777,216 samples at 4 blocks per sample; sample it more coarsely or make it smaller."], _analysis.FindBiomeProblems(new BiomeCensusRequest(world, SearchArea.Square(20_000), _reference, 4, 64)));
        Assert.Equal(["Trial chambers does not generate in this version."], _analysis.FindStructureProblems(StructureType.TrialChambers, McVersion.V1_16_5, SearchArea.Square(1000)));
        Assert.Equal(["Villages form no quads in this version."], _analysis.FindQuadProblems(StructureType.Village, McVersion.V1_16_5, SearchArea.Square(1000)));
        Assert.Empty(_analysis.FindStructureProblems(StructureType.Village, McVersion.V1_16_5, SearchArea.Square(100_000)));
        Assert.Single(_analysis.FindStructureProblems(StructureType.Village, McVersion.V1_16_5, SearchArea.Square(200_000)));
    }

    /// <summary>Generates the whole grid of an area at once and counts each sample inside it.</summary>
    private BiomeCensus CountDirectly(BiomeCensusRequest request)
    {
        var scale = request.Scale;
        var blocks = request.Area.ToBlocks(request.Reference);
        var (left, top) = (FloorDivide(blocks.X, scale), FloorDivide(blocks.Z, scale));
        var (sizeX, sizeZ) = (FloorDivide(blocks.Right - 1, scale) - left + 1, FloorDivide(blocks.Bottom - 1, scale) - top + 1);
        var biomes = new byte[sizeX * sizeZ];
        _bench.Open(new SearchDefinition { Version = request.World.Version }, request.World.Seed).GetGenerator(Dimension.Overworld)
            .GenerateBiomes(new BiomeArea(scale, left, top, sizeX, sizeZ, scale == 1 ? request.Y : request.Y >> 2), biomes);

        var samples = 0;
        Dictionary<int, BiomeTally> tallies = [];
        for (var index = 0; index < biomes.Length; index++)
        {
            var position = new BlockPos(
                Math.Clamp((left + index % sizeX) * scale + scale / 2, blocks.X, blocks.Right - 1),
                Math.Clamp((top + index / sizeX) * scale + scale / 2, blocks.Z, blocks.Bottom - 1));
            if (biomes[index] == 255 || !request.Area.Contains(request.Reference, position))
                continue;

            samples++;
            var distance = position.SquaredDistanceTo(request.Reference);
            tallies[biomes[index]] = tallies.TryGetValue(biomes[index], out var tally)
                ? (distance < tally.NearestSquaredDistance ? tally with { Samples = tally.Samples + 1, Nearest = position, NearestSquaredDistance = distance } : tally with { Samples = tally.Samples + 1 })
                : new BiomeTally(biomes[index], 1, position, distance);
        }

        return new BiomeCensus { Samples = samples, Tallies = [.. tallies.Values.OrderBy(tally => tally.Biome)] };
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}
