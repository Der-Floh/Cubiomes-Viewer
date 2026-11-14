using Cubiomes_Viewer.Engine.Data.Locate;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.LocateServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.LocateServ;

/// <summary>Finding the nearest place on the real workers, against looking at every place of the range directly with one generator.</summary>
public sealed class LocateServiceTests : IDisposable
{
    private const long Seed = 262;
    private const int BiomeCell = 4;
    private const int TheEnd = 9;
    private static readonly BlockPos _origin = new(300, -200);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    private readonly ILocateService _locate;
    private readonly IStructureFinder _structureFinder;
    private readonly ConditionTestBench _bench = new();

    public LocateServiceTests()
    {
        _services = new ServiceCollection().AddLogging().AddCubiomesEngine().AddSingleton<IAppDataStore>(new FileSystemAppDataStore(_directory)).BuildServiceProvider();
        _locate = _services.GetRequiredService<ILocateService>();
        _structureFinder = _services.GetRequiredService<IStructureFinder>();
    }

    public void Dispose()
    {
        _services.Dispose();
        _bench.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    public static TheoryData<StructureType, McVersion, int, int, int> Structures() => new()
    {
        { StructureType.Village, McVersion.V1_21_3, 300, -200, 3000 },
        { StructureType.Village, McVersion.V1_16_5, -2176, 1088, 3000 },
        { StructureType.Fortress, McVersion.V1_21_3, 40, 40, 2000 },
        { StructureType.Bastion, McVersion.V1_18_2, -431, 0, 2000 },
        { StructureType.EndCity, McVersion.V1_21_3, 2000, 0, 2000 },
        { StructureType.Treasure, McVersion.V1_21_3, 0, 0, 800 },
        { StructureType.Geode, McVersion.V1_21_3, -15, 17, 400 },
        { StructureType.Mineshaft, McVersion.V1_12_2, 1, 1, 400 },
    };

    [Theory]
    [MemberData(nameof(Structures))]
    public async Task FindStructureAsync_Type_IsTheNearestTheGeneratorFindsInTheRange(StructureType type, McVersion version, int x, int z, int range)
    {
        var origin = new BlockPos(x, z);

        var found = await _locate.FindStructureAsync(World(version), type, origin, range, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(FindStructureDirectly(type, version, origin, range), found);
    }

    [Fact]
    public async Task FindStructureAsync_GameRange_FindsTheNearestVillage()
    {
        var range = _locate.GetStructureRange(StructureType.Village, McVersion.V1_21_3);

        var found = await _locate.FindStructureAsync(World(McVersion.V1_21_3), StructureType.Village, _origin, range, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(FindStructureDirectly(StructureType.Village, McVersion.V1_21_3, _origin, (int)Math.Ceiling(found.Distance) + 1), found);
    }

    public static TheoryData<McVersion, Dimension> BiomeWorlds() => new()
    {
        { McVersion.V1_21_3, Dimension.Overworld },
        { McVersion.V1_16_5, Dimension.Overworld },
        { McVersion.V1_21_3, Dimension.Nether },
        { McVersion.V1_21_3, Dimension.End },
    };

    [Theory]
    [MemberData(nameof(BiomeWorlds))]
    public async Task FindBiomeAsync_Biome_IsTheNearestCellTheGeneratorShows(McVersion version, Dimension dimension)
    {
        const int Range = 1200;
        var cells = GenerateCellsAround(version, dimension, _origin, Range);
        var biome = cells.First(cell => cell.Position == new BlockPos(_origin.X + 602, _origin.Z + 402)).Biome;

        var found = await _locate.FindBiomeAsync(World(version, dimension), biome, ViewSettings.DefaultSampleHeight, _origin, Range, cancellationToken: TestContext.Current.CancellationToken);

        var expected = cells.Where(cell => cell.Biome == biome)
            .Select(cell => new LocateResult(cell.Position, cell.Position.SquaredDistanceTo(_origin)))
            .Where(result => result.SquaredDistance <= (long)Range * Range)
            .OrderBy(result => result.SquaredDistance).ThenBy(result => result.Position.Z).ThenBy(result => result.Position.X)
            .First();
        Assert.Equal(expected, found);
    }

    [Fact]
    public async Task FindBiomeAsync_NoneInTheRange_FindsNothingAndReportsTheWholeRange()
    {
        const int Range = 600;
        List<int> covered = [];

        var found = await _locate.FindBiomeAsync(World(McVersion.V1_21_3), TheEnd, ViewSettings.DefaultSampleHeight, _origin, Range, new SynchronousProgress(covered.Add), TestContext.Current.CancellationToken);

        Assert.Null(found);
        Assert.Equal(covered.Order(), covered);
        Assert.Equal(Range, covered[^1]);
    }

    [Fact]
    public async Task FindStructureAsync_Progress_RisesUntilTheFoundOneIsCovered()
    {
        List<int> covered = [];

        var found = await _locate.FindStructureAsync(World(McVersion.V1_21_3), StructureType.Village, _origin, 6000, new SynchronousProgress(covered.Add), TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.NotEmpty(covered);
        Assert.Equal(covered.Order(), covered);
        Assert.True(covered[^1] >= found.Distance, $"The search covered {covered[^1]} blocks but found the village {found.Distance} blocks away.");
    }

    [Fact]
    public async Task FindStrongholdAsync_World_IsTheNearestOfItsStrongholds()
    {
        var strongholds = _bench.GetGenerator(McVersion.V1_21_3, Seed).FindStrongholds();

        var found = await _locate.FindStrongholdAsync(World(McVersion.V1_21_3, Dimension.Nether), _origin, TestContext.Current.CancellationToken);

        var expected = strongholds.Select((position, index) => new LocateResult(position, position.SquaredDistanceTo(_origin), index))
            .OrderBy(result => result.SquaredDistance).ThenBy(result => result.Position.Z).ThenBy(result => result.Position.X)
            .First();
        Assert.Equal(expected, found);
    }

    [Fact]
    public async Task FindStrongholdAsync_VersionWithoutStrongholds_FindsNothing()
        => Assert.Null(await _locate.FindStrongholdAsync(World(McVersion.Beta1_7), _origin, TestContext.Current.CancellationToken));

    [Fact]
    public async Task FindBiomeAsync_Cancelled_ThrowsWithoutAnAnswer()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _locate.FindBiomeAsync(World(McVersion.V1_21_3), TheEnd, ViewSettings.DefaultSampleHeight, _origin, 600, cancellationToken: cancelled.Token));
    }

    [Theory]
    [InlineData(StructureType.Village, 54_400)]
    [InlineData(StructureType.Mansion, 128_000)]
    [InlineData(StructureType.Treasure, 6_400)]
    public void GetStructureRange_Type_IsAHundredRegionsButAtLeastTheBiomeRange(StructureType type, int expected)
    {
        Assert.Equal(expected, _locate.GetStructureRange(type, McVersion.V1_21_3));
        Assert.Equal(6_400, _locate.BiomeRange);
    }

    private static WorldKey World(McVersion version, Dimension dimension = Dimension.Overworld) => new(Seed, version, dimension, new SearchDefinition().Flags);

    private LocateResult? FindStructureDirectly(StructureType type, McVersion version, BlockPos origin, int range)
    {
        Assert.True(_structureFinder.TryGetPlacement(type, version, out var placement));
        var generator = _bench.GetGenerator(version, Seed, placement.Dimension);
        var area = new BlockArea(origin.X - range, origin.Z - range, 2 * range + 1, 2 * range + 1);

        return generator.FindStructures(type, area)
            .Select(position => new LocateResult(position, position.SquaredDistanceTo(origin)))
            .Where(result => result.SquaredDistance <= (long)range * range)
            .OrderBy(result => result.SquaredDistance).ThenBy(result => result.Position.Z).ThenBy(result => result.Position.X)
            .FirstOrDefault();
    }

    /// <summary>Every biome cell of 4 by 4 blocks around a block, generated as one area at the default height, with the middle of each cell.</summary>
    private List<(BlockPos Position, int Biome)> GenerateCellsAround(McVersion version, Dimension dimension, BlockPos origin, int range)
    {
        var (left, top) = (FloorDivide(origin.X - range, BiomeCell), FloorDivide(origin.Z - range, BiomeCell));
        var side = FloorDivide(origin.X + range, BiomeCell) - left + 1;
        var area = new BiomeArea(BiomeCell, left, top, side, side, ViewSettings.DefaultSampleHeight >> 2);
        var biomes = new byte[area.SampleCount];
        _bench.GetGenerator(version, Seed, dimension).GenerateBiomes(area, biomes);

        return [.. biomes.Select((biome, index) => (new BlockPos((left + index % side) * BiomeCell + BiomeCell / 2, (top + index / side) * BiomeCell + BiomeCell / 2), (int)biome))];
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

    private sealed class SynchronousProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}
