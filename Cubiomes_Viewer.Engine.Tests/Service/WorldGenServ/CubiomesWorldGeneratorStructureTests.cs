using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

public sealed class CubiomesWorldGeneratorStructureTests
{
    private const long Seed = 262;
    private const int ChunkBlocks = 16;
    private const int WideAreaBlocks = 4096;
    private const int DenseAreaBlocks = 1024;
    private const int FirstRingMinBlocks = 1280 - 112;
    private const int FirstRingMaxBlocks = 2816 + 112;

    private readonly CubiomesStructureFinder _finder = new();

    public static TheoryData<McVersion, Dimension, StructureType> LocatableStructures() => new()
    {
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.Village },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.DesertPyramid },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.JungleTemple },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.Monument },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.Mansion },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.Outpost },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.Shipwreck },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.OceanRuin },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.RuinedPortal },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.AncientCity },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.TrailRuins },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.TrialChambers },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.Treasure },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.Mineshaft },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.DesertWell },
        { McVersion.V1_21_3, Dimension.Overworld, StructureType.Geode },
        { McVersion.V1_21_3, Dimension.Nether, StructureType.Fortress },
        { McVersion.V1_21_3, Dimension.Nether, StructureType.Bastion },
        { McVersion.V1_21_3, Dimension.Nether, StructureType.RuinedPortalNether },
        { McVersion.V1_21_3, Dimension.End, StructureType.EndCity },
        { McVersion.V1_21_3, Dimension.End, StructureType.EndGateway },
        { McVersion.V1_16_5, Dimension.Overworld, StructureType.Village },
        { McVersion.V1_16_5, Dimension.Overworld, StructureType.SwampHut },
        { McVersion.V1_16_5, Dimension.Overworld, StructureType.Monument },
        { McVersion.V1_16_5, Dimension.Nether, StructureType.Fortress },
        { McVersion.V1_16_5, Dimension.Nether, StructureType.Bastion },
        { McVersion.V1_12_2, Dimension.Overworld, StructureType.Village },
        { McVersion.V1_12_2, Dimension.Overworld, StructureType.Igloo },
        { McVersion.V1_12_2, Dimension.Overworld, StructureType.Mansion },
        { McVersion.V1_12_2, Dimension.Overworld, StructureType.Mineshaft },
        { McVersion.V1_12_2, Dimension.Nether, StructureType.Fortress },
        { McVersion.V1_12_2, Dimension.End, StructureType.EndCity },
        { McVersion.Beta1_8, Dimension.Overworld, StructureType.Village },
    };

    [Theory]
    [MemberData(nameof(LocatableStructures))]
    public void FindStructures_AnyStructure_MatchesScanningEveryRegion(McVersion version, Dimension dimension, StructureType type)
    {
        using var generator = CreateGenerator(version, dimension);
        Assert.True(_finder.TryGetPlacement(type, version, out var placement));
        Assert.Equal(dimension, placement.Dimension);
        var area = CenteredArea(placement.RegionBlocks == ChunkBlocks ? DenseAreaBlocks : WideAreaBlocks);

        var found = generator.FindStructures(type, area);

        Assert.Equal(ScanRegions(generator, type, placement, area), found);
    }

    [Fact]
    public void FindStructures_RuinedPortals_AreFoundInEveryRegion()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        Assert.True(_finder.TryGetPlacement(StructureType.RuinedPortal, McVersion.V1_21_3, out var placement));
        var area = new BlockArea(0, 0, placement.RegionBlocks * 3, placement.RegionBlocks * 2);

        var found = generator.FindStructures(StructureType.RuinedPortal, area);

        Assert.Equal(6, found.Length);
        Assert.All(found, position => Assert.True(area.Contains(position)));
    }

    [Fact]
    public void FindStructures_NeighbouringAreas_ShareNoStructureAndMissNone()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var whole = new BlockArea(-3000, -1000, 6000, 2000);
        var left = new BlockArea(-3000, -1000, 2777, 2000);
        var right = new BlockArea(-223, -1000, 3223, 2000);

        var expected = generator.FindStructures(StructureType.Shipwreck, whole);
        var parts = generator.FindStructures(StructureType.Shipwreck, left).Concat(generator.FindStructures(StructureType.Shipwreck, right));

        Assert.NotEmpty(expected);
        Assert.Equal(expected.Order(PositionOrder), parts.Order(PositionOrder));
    }

    [Fact]
    public void FindStructures_StructureNotInVersion_ReturnsNothing()
    {
        using var generator = CreateGenerator(McVersion.V1_12_2, Dimension.Overworld);

        Assert.Empty(generator.FindStructures(StructureType.TrialChambers, CenteredArea(WideAreaBlocks)));
    }

    [Fact]
    public void FindStructures_OtherDimensionApplied_ThrowsInvalidState()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.FindStructures(StructureType.Fortress, CenteredArea(WideAreaBlocks)));

        Assert.Equal(NativeStatus.InvalidState, exception.Status);
    }

    [Theory]
    [InlineData(StructureType.Feature, McVersion.V1_12_2, Dimension.Overworld)]
    [InlineData(StructureType.EndIsland, McVersion.V1_21_3, Dimension.End)]
    public void FindStructures_TypeWithoutViabilityCheck_ThrowsUnsupported(StructureType type, McVersion version, Dimension dimension)
    {
        using var generator = CreateGenerator(version, dimension);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.FindStructures(type, CenteredArea(DenseAreaBlocks)));

        Assert.Equal(NativeStatus.Unsupported, exception.Status);
    }

    [Theory]
    [InlineData(McVersion.V1_21_3, 128)]
    [InlineData(McVersion.V1_12_2, 128)]
    [InlineData(McVersion.V1_8_9, 3)]
    [InlineData(McVersion.Beta1_7, 0)]
    public void FindStrongholds_AnyVersion_ReturnsEveryStrongholdOfTheWorld(McVersion version, int expectedCount)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld);

        var strongholds = generator.FindStrongholds();

        Assert.Equal(expectedCount, strongholds.Length);
        Assert.Equal(strongholds, generator.FindStrongholds());
    }

    [Fact]
    public void FindStrongholds_FirstRing_LiesAtItsDistanceFromTheOrigin()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);

        var firstRing = generator.FindStrongholds().Take(3);

        Assert.All(firstRing, position => Assert.InRange(double.Hypot(position.X, position.Z), FirstRingMinBlocks, FirstRingMaxBlocks));
    }

    [Fact]
    public void FindStrongholds_NetherApplied_ThrowsInvalidState()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Nether);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.FindStrongholds());

        Assert.Equal(NativeStatus.InvalidState, exception.Status);
    }

    [Theory]
    [InlineData(McVersion.V1_21_3)]
    [InlineData(McVersion.V1_17_1)]
    [InlineData(McVersion.V1_12_2)]
    public void FindSpawn_PredictableVersion_ReturnsTheSamePointEveryTime(McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld);

        var spawn = generator.FindSpawn();

        Assert.NotNull(spawn);
        Assert.Equal(spawn, generator.FindSpawn());
    }

    [Fact]
    public void FindSpawn_Beta17_ReturnsNull()
    {
        using var generator = CreateGenerator(McVersion.Beta1_7, Dimension.Overworld);

        Assert.Null(generator.FindSpawn());
    }

    private static CubiomesWorldGenerator CreateGenerator(McVersion version, Dimension dimension)
    {
        var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, Seed);
        return generator;
    }

    private static BlockArea CenteredArea(int sideBlocks) => new(-sideBlocks / 2, -sideBlocks / 2, sideBlocks, sideBlocks);

    private static Comparer<BlockPos> PositionOrder { get; } = Comparer<BlockPos>.Create(
        static (left, right) => left.X != right.X ? left.X.CompareTo(right.X) : left.Z.CompareTo(right.Z));

    private BlockPos[] ScanRegions(CubiomesWorldGenerator generator, StructureType type, StructurePlacement placement, BlockArea area)
    {
        List<BlockPos> found = [];
        for (var regionZ = FloorDivide(area.Z, placement.RegionBlocks); regionZ <= FloorDivide(area.Bottom - 1, placement.RegionBlocks); regionZ++)
        {
            for (var regionX = FloorDivide(area.X, placement.RegionBlocks); regionX <= FloorDivide(area.Right - 1, placement.RegionBlocks); regionX++)
            {
                if (_finder.TryGetRegionPosition(type, generator.Version, Seed, regionX, regionZ, out var position)
                    && area.Contains(position)
                    && generator.CanGenerateStructure(type, position))
                {
                    found.Add(position);
                }
            }
        }

        return [.. found];
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}
