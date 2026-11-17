using Cubiomes_Viewer.Engine.Data;
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

    [Theory]
    [InlineData(87, 87)]
    [InlineData(33, 33)]
    public void Find_NetherFossil_LiesBetweenTheLavaSeaAndTheHeightTheGeneratorPicked(int picked, int highest)
    {
        using var generator = CreateGenerator(Version, Dimension.Nether);

        var height = _service.Find(generator, Dimension.Nether, Describe(StructureType.NetherFossil, new StructureVariant { Height = picked }));

        Assert.Equal(StructureHeight.Between(33, highest), height);
    }

    [Fact]
    public void Find_NetherFossilPickedAtTheLavaSea_HasNoHeight()
    {
        using var generator = CreateGenerator(Version, Dimension.Nether);

        Assert.Null(_service.Find(generator, Dimension.Nether, Describe(StructureType.NetherFossil, new StructureVariant { Height = 32 })));
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
    public void Find_Village_IsAnEstimateOfTheSurfaceUnderItsStartPiece()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);
        var details = generator.DescribeStructure(StructureType.Village, generator.FindStructures(StructureType.Village, _searchArea)[0]);
        var box = details.Variant?.Box ?? throw new InvalidOperationException("The village has no start piece.");

        var height = _service.Find(generator, Dimension.Overworld, details);

        Assert.NotNull(height);
        Assert.True(height.IsEstimate);
        Assert.Equal(GetSurface(generator, new BlockPos((box.MinX + box.MaxX) / 2, (box.MinZ + box.MaxZ) / 2)), height.Lowest);
        Assert.InRange(height.Lowest, 40, 200);
    }

    [Fact]
    public void Find_TrailRuins_LieBuriedBelowTheSurface()
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);

        var ruins = _service.Find(generator, Dimension.Overworld, Describe(StructureType.TrailRuins));

        Assert.Equal(StructureHeight.Around(GetSurface(generator, _position) - 15), ruins);
    }

    [Fact]
    public void Find_EndCity_IsAnEstimateOfTheIslandItStandsOn()
    {
        using var generator = CreateGenerator(Version, Dimension.End);
        var city = generator.FindStructures(StructureType.EndCity, _searchArea)[0];

        var height = _service.Find(generator, Dimension.End, generator.DescribeStructure(StructureType.EndCity, city));

        Assert.NotNull(height);
        Assert.True(height.IsEstimate);
        Assert.InRange(height.Lowest, 40, 128);
    }

    [Fact]
    public void Find_EndStructureOverTheVoid_HasNoHeight()
    {
        using var generator = CreateGenerator(Version, Dimension.End);
        var voidPosition = new BlockPos(480, 0);

        Assert.Equal(0, GetSurface(generator, voidPosition));
        Assert.Null(_service.Find(generator, Dimension.End, Describe(StructureType.EndCity, position: voidPosition)));
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
    public void Find_StructureWithoutAHeightToTell_HasNoHeight(StructureType type)
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);

        Assert.Null(_service.Find(generator, Dimension.Overworld, Describe(type, new StructureVariant { Height = 12 })));
    }

    [Theory]
    [MemberData(nameof(StructureTypes))]
    public void Find_AnyStructureType_IsHandled(StructureType type)
    {
        using var generator = CreateGenerator(Version, Dimension.Overworld);

        var exception = Record.Exception(() => _service.Find(generator, Dimension.Overworld, Describe(type)));

        Assert.Null(exception);
    }

    private static CubiomesWorldGenerator CreateGenerator(McVersion version, Dimension dimension)
    {
        var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, Seed);
        return generator;
    }

    private static StructureDetails Describe(StructureType type, StructureVariant? variant = null, StructurePiece[]? pieces = null, BlockPos? position = null)
        => new() { Type = type, Position = position ?? _position, Variant = variant, Pieces = pieces ?? [] };

    private static StructurePiece Piece(int minY, int maxY) => new(0, 0, 0, new BlockBox(0, minY, 0, 18, maxY, 18));

    private static int GetSurface(IWorldGenerator generator, BlockPos position)
    {
        var surface = new short[1];
        generator.GenerateHeights(new BiomeArea(4, position.X >> 2, position.Z >> 2, 1, 1, 0), surface);
        return (int)Math.Round(surface[0] / (double)Constants.HeightUnit, MidpointRounding.AwayFromZero);
    }
}
