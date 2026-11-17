using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

public sealed class StructureHeightServiceTests : IDisposable
{
    private const long Seed = 262;
    private const McVersion Version = McVersion.V1_21_3;
    private const int SeaLevel = 63;
    private static readonly BlockPos _position = new(160, -320);
    private static readonly BlockArea _searchArea = new(-4096, -4096, 8192, 8192);

    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddCubiomesEngine().BuildServiceProvider();
    private readonly IStructureHeightService _service;

    public StructureHeightServiceTests()
    {
        _service = _services.GetRequiredService<IStructureHeightService>();
    }

    public void Dispose() => _services.Dispose();

    public static TheoryData<StructureType> StructureTypes => [.. Enum.GetValues<StructureType>()];

    [Theory]
    [InlineData(StructureType.AncientCity, Dimension.Overworld, -51)]
    [InlineData(StructureType.Monument, Dimension.Overworld, 39)]
    [InlineData(StructureType.Bastion, Dimension.Nether, 33)]
    public void Find_StructureTheGamePlacesAtAFixedHeight_IsThatHeight(StructureType type, Dimension dimension, int y)
    {
        using var generator = CreateGenerator(Version, dimension);

        Assert.Equal(StructureHeight.At(y), _service.Find(generator, dimension, Describe(type)));
    }

    [Fact]
    public void Find_TrialChambers_IsTheHeightTheGeneratorPicked()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);
        var details = generator.DescribeStructure(StructureType.TrialChambers, generator.FindStructures(StructureType.TrialChambers, _searchArea)[0]);
        var picked = details.Variant?.Height ?? throw new InvalidOperationException("The generator picked no height for the trial chambers.");

        var height = _service.Find(generator, Dimension.Overworld, details);

        Assert.Equal(StructureHeight.At(picked), height);
        Assert.InRange(picked, -40, -20);
    }

    [Fact]
    public void Find_GeodeAndTrialChambersWithoutAVariantHeight_HaveNoHeight()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);

        Assert.Null(_service.Find(generator, Dimension.Overworld, Describe(StructureType.TrialChambers, new StructureVariant())));
        Assert.Null(_service.Find(generator, Dimension.Overworld, Describe(StructureType.Geode)));
    }

    [Fact]
    public void Find_Geode_IsAroundTheMiddleTheGeneratorPicked()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);

        Assert.Equal(StructureHeight.Around(-20), _service.Find(generator, Dimension.Overworld, Describe(StructureType.Geode, new StructureVariant { Height = -20 })));
    }

    [Fact]
    public void Find_NetherFossil_IsTheFloorItSettlesOn()
    {
        using var generator = CreateGenerator(Version, Dimension.Nether);
        var fossil = generator.FindStructures(StructureType.NetherFossil, _searchArea)[0];

        var height = _service.Find(generator, Dimension.Nether, generator.DescribeStructure(StructureType.NetherFossil, fossil));

        Assert.Equal(StructureHeight.At(Assert.NotNull(generator.FindStructureFloor(StructureType.NetherFossil, fossil))), height);
    }

    [Fact]
    public void Find_Fortress_IsTheRangeTheGameCanMoveItsStartPieceTo()
    {
        using var generator = CreateGenerator(Version, Dimension.Nether);
        StructurePiece[] pieces = [Piece(64, 73), Piece(61, 69), Piece(66, 71)];

        var height = _service.Find(generator, Dimension.Nether, Describe(StructureType.Fortress, pieces: pieces));

        Assert.Equal(StructureHeight.Between(51, 60), height);
    }

    [Fact]
    public void Find_FortressTooTallToMove_IsTheOneHeightItsBottomLandsAt()
    {
        using var generator = CreateGenerator(Version, Dimension.Nether);
        StructurePiece[] pieces = [Piece(64, 73), Piece(40, 50)];

        var height = _service.Find(generator, Dimension.Nether, Describe(StructureType.Fortress, pieces: pieces));

        Assert.Equal(StructureHeight.At(72), height);
    }

    [Fact]
    public void Find_GeneratedFortress_LiesWhereTheGameKeepsFortresses()
    {
        using var generator = CreateGenerator(Version, Dimension.Nether);
        var details = generator.DescribeStructure(StructureType.Fortress, generator.FindStructures(StructureType.Fortress, _searchArea)[0]);

        var height = _service.Find(generator, Dimension.Nether, details);

        Assert.NotNull(height);
        Assert.False(height.IsEstimate);
        Assert.InRange(height.Lowest, 48, height.Highest);
        Assert.InRange(height.Highest - height.Lowest, 0, 70 - 48);
    }

    [Fact]
    public void Find_Village_IsTheSurfaceUnderTheMiddleOfItsStartPiece()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);
        var details = generator.DescribeStructure(StructureType.Village, generator.FindStructures(StructureType.Village, _searchArea)[0]);
        var box = details.Variant?.Box ?? throw new InvalidOperationException("The village has no start piece.");

        var height = _service.Find(generator, Dimension.Overworld, details);

        Assert.Equal(StructureHeight.At(ReadSurface(generator, (box.MinX + box.MaxX) / 2, (box.MinZ + box.MaxZ) / 2)), height);
    }

    [Fact]
    public void Find_VillageBefore114_IsAnEstimate()
    {
        using var generator = CreateGenerator(McVersion.V1_13_2, Dimension.Overworld);
        var details = generator.DescribeStructure(StructureType.Village, generator.FindStructures(StructureType.Village, _searchArea)[0]);

        var height = _service.Find(generator, Dimension.Overworld, details);

        Assert.True(height?.IsEstimate);
    }

    [Theory]
    [InlineData(McVersion.V1_16_5)]
    [InlineData(McVersion.V1_12_2)]
    public void Find_DesertPyramidBefore118_StandsAtY64(McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld);

        Assert.Equal(StructureHeight.At(64), _service.Find(generator, Dimension.Overworld, Describe(StructureType.DesertPyramid)));
    }

    [Fact]
    public void Find_DesertPyramid_SinksUpToTwoBlocksBelowTheLowestGroundUnderIt()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);
        var pyramid = generator.FindStructures(StructureType.DesertPyramid, new BlockArea(-16384, -16384, 32768, 32768))[0];
        var corner = new BlockPos(pyramid.X & ~15, pyramid.Z & ~15);
        var lowest = Enumerable.Range(0, 21 * 21).Min(index => ReadSurface(generator, corner.X + index % 21, corner.Z + index / 21));

        var height = _service.Find(generator, Dimension.Overworld, generator.DescribeStructure(StructureType.DesertPyramid, pyramid));

        Assert.Equal(StructureHeight.Between(lowest - 2, lowest), height);
    }

    [Fact]
    public void Find_JungleTemple_IsTheAverageGroundUnderIt()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);
        var details = generator.DescribeStructure(StructureType.JungleTemple, generator.FindStructures(StructureType.JungleTemple, _searchArea)[0]);
        var box = details.Variant?.Box ?? throw new InvalidOperationException("The jungle temple has no footprint.");
        var columns = Enumerable.Range(0, box.SizeX * box.SizeZ).Select(index => ReadSurface(generator, box.MinX + index % box.SizeX, box.MinZ + index / box.SizeX)).ToArray();

        var height = _service.Find(generator, Dimension.Overworld, details);

        Assert.Equal(StructureHeight.At(columns.Sum() / columns.Length), height);
    }

    [Fact]
    public void Find_SwampHutBefore120_IsAnEstimateOfTheGroundUnderIt()
    {
        using var generator = CreateGenerator(McVersion.V1_16_5, Dimension.Overworld);
        var details = generator.DescribeStructure(StructureType.SwampHut, generator.FindStructures(StructureType.SwampHut, _searchArea)[0]);

        var height = _service.Find(generator, Dimension.Overworld, details);

        Assert.True(height?.IsEstimate);
        Assert.InRange(height.Lowest, SeaLevel, 100);
    }

    [Fact]
    public void Find_Mansion_IsTheFloorTheTerrainGivesIt()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);
        var mansion = generator.FindStructures(StructureType.Mansion, new BlockArea(-16384, -16384, 32768, 32768))
            .First(position => generator.FindStructureFloor(StructureType.Mansion, position) is not null);

        var height = _service.Find(generator, Dimension.Overworld, generator.DescribeStructure(StructureType.Mansion, mansion));

        Assert.Equal(StructureHeight.At(Assert.NotNull(generator.FindStructureFloor(StructureType.Mansion, mansion))), height);
    }

    [Fact]
    public void Find_TrailRuins_LieBuriedBelowTheSurface()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);

        var ruins = _service.Find(generator, Dimension.Overworld, Describe(StructureType.TrailRuins));

        Assert.Equal(StructureHeight.Around(ReadSurface(generator, _position.X, _position.Z) - 15), ruins);
    }

    [Fact]
    public void Find_OceanRuin_IsAnEstimateOfTheSeaFloorBelowTheWater()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);
        var ruin = generator.FindStructures(StructureType.OceanRuin, _searchArea)[0];

        var height = _service.Find(generator, Dimension.Overworld, generator.DescribeStructure(StructureType.OceanRuin, ruin));

        Assert.True(height?.IsEstimate);
        Assert.InRange(height.Lowest, -64, SeaLevel + 8);
    }

    [Fact]
    public void Find_EndCity_IsTheFloorTheTerrainGivesIt()
    {
        using var generator = CreateGenerator(Version, Dimension.End);
        var city = generator.FindStructures(StructureType.EndCity, _searchArea)[0];

        var height = _service.Find(generator, Dimension.End, generator.DescribeStructure(StructureType.EndCity, city));

        Assert.Equal(StructureHeight.At(Assert.NotNull(generator.FindStructureFloor(StructureType.EndCity, city))), height);
    }

    [Fact]
    public void Find_EndStructureOverTheVoid_HasNoHeight()
    {
        using var generator = CreateGenerator(Version, Dimension.End);

        Assert.Null(_service.Find(generator, Dimension.End, Describe(StructureType.EndGateway, position: new BlockPos(480, 0))));
        Assert.Null(_service.Find(generator, Dimension.End, Describe(StructureType.EndCity, position: new BlockPos(480, 0))));
    }

    [Fact]
    public void Find_SurfaceStructureInAVersionWithoutHeights_HasNoHeight()
    {
        using var generator = CreateGenerator(McVersion.Beta1_7, Dimension.Overworld);

        Assert.Null(_service.Find(generator, Dimension.Overworld, Describe(StructureType.Village)));
    }

    [Theory]
    [InlineData(StructureType.Mineshaft)]
    [InlineData(StructureType.RuinedPortal)]
    [InlineData(StructureType.RuinedPortalNether)]
    [InlineData(StructureType.EndGateway)]
    [InlineData(StructureType.EndIsland)]
    [InlineData(StructureType.Stronghold)]
    public void Find_StructureWithoutAHeightToTell_HasNoHeight(StructureType type)
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);

        Assert.Null(_service.Find(generator, Dimension.Overworld, Describe(type, new StructureVariant { Height = 12 })));
    }

    [Theory]
    [MemberData(nameof(StructureTypes))]
    public void Find_AnyStructureType_IsHandled(StructureType type)
    {
        var dimension = GetDimension(type);
        using var generator = CreateGenerator(Version, dimension);

        var exception = Record.Exception(() => _service.Find(generator, dimension, Describe(type)));

        Assert.Null(exception);
    }

    private static Dimension GetDimension(StructureType type) => type switch
    {
        StructureType.Fortress or StructureType.Bastion or StructureType.NetherFossil or StructureType.RuinedPortalNether => Dimension.Nether,
        StructureType.EndCity or StructureType.EndGateway or StructureType.EndIsland => Dimension.End,
        _ => Dimension.Overworld,
    };

    private static CubiomesWorldGenerator CreateGenerator(McVersion version, Dimension dimension)
    {
        var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, Seed);
        return generator;
    }

    private static StructureDetails Describe(StructureType type, StructureVariant? variant = null, StructurePiece[]? pieces = null, BlockPos? position = null)
        => new() { Type = type, Position = position ?? _position, Variant = variant, Pieces = pieces ?? [] };

    private static StructurePiece Piece(int minY, int maxY) => new(0, 0, 0, new BlockBox(0, minY, 0, 18, maxY, 18));

    /// <summary>The surface the game's height maps see in the Overworld: the ground, or the sea above it.</summary>
    private static int ReadSurface(IWorldGenerator generator, int x, int z)
    {
        var surface = new short[1];
        generator.GenerateSurface(new BlockArea(x, z, 1, 1), surface);
        return Math.Max((int)surface[0], SeaLevel);
    }
}
