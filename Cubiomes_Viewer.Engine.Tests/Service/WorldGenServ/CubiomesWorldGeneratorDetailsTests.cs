using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

public sealed class CubiomesWorldGeneratorDetailsTests
{
    private const long Seed = 262;
    private const int ChunkBlocks = 16;
    private const int FortressStartPiece = 0;
    private const int MonumentSide = 58;
    private const int GatewayRingBlocks = 96;
    private static readonly BlockArea _area = new(-4096, -4096, 8192, 8192);
    private static readonly int[] _villageBiomes = [1, 2, 5, 12, 35];

    [Fact]
    public void DescribeStructure_EndCity_ReturnsPiecesThatStartInItsChunk()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.End);
        var city = generator.FindStructures(StructureType.EndCity, _area)[0];

        var details = generator.DescribeStructure(StructureType.EndCity, city);

        Assert.Equal(StructureType.EndCity, details.Type);
        Assert.Equal(city, details.Position);
        Assert.Null(details.Variant);
        Assert.NotEmpty(details.Pieces);
        Assert.True(Contains(details.Pieces[0].Box, city.X + ChunkBlocks / 2, city.Z + ChunkBlocks / 2));
        Assert.All(details.Pieces, piece => Assert.True(piece.Box.MaxX >= piece.Box.MinX && piece.Box.MaxY >= piece.Box.MinY && piece.Box.MaxZ >= piece.Box.MinZ));
    }

    [Fact]
    public void DescribeStructure_EndCities_SomeHaveAShipAndSomeDoNot()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.End);

        var withShip = generator.FindStructures(StructureType.EndCity, _area)
            .Select(city => generator.DescribeStructure(StructureType.EndCity, city).Pieces.Any(piece => piece.Type == EndCityPiece.Ship))
            .ToArray();

        Assert.Contains(true, withShip);
        Assert.Contains(false, withShip);
    }

    [Theory]
    [InlineData(McVersion.V1_21_3)]
    [InlineData(McVersion.V1_12_2)]
    public void DescribeStructure_Fortress_StartsWithTheStartPieceInItsChunk(McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.Nether);
        var fortress = generator.FindStructures(StructureType.Fortress, _area)[0];

        var details = generator.DescribeStructure(StructureType.Fortress, fortress);

        Assert.NotEmpty(details.Pieces);
        Assert.Equal(FortressStartPiece, details.Pieces[0].Type);
        Assert.Equal(fortress.X >> 4, details.Pieces[0].Box.MinX >> 4);
        Assert.Equal(fortress.Z >> 4, details.Pieces[0].Box.MinZ >> 4);
        Assert.Contains(details.Pieces, piece => piece.Type == FortressPiece.BlazeSpawner);
    }

    [Fact]
    public void DescribeStructure_Village_ReportsItsBiomeVariantAndFirstPiece()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var villages = generator.FindStructures(StructureType.Village, _area);

        Assert.NotEmpty(villages);
        Assert.All(villages, village =>
        {
            var variant = generator.DescribeStructure(StructureType.Village, village).Variant;

            Assert.NotNull(variant);
            Assert.Contains(variant.Biome, _villageBiomes);
            Assert.True(variant.Start >= 0);
            Assert.InRange(variant.Rotation, 0, 3);
            var box = Assert.NotNull(variant.Box);
            Assert.InRange(box.MinX, village.X - 32, village.X + 32);
            Assert.InRange(box.MinZ, village.Z - 32, village.Z + 32);
        });
    }

    [Fact]
    public void DescribeStructure_Monument_ReportsItsFootprintAroundThePosition()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var monument = generator.FindStructures(StructureType.Monument, _area)[0];

        var box = Assert.NotNull(generator.DescribeStructure(StructureType.Monument, monument).Variant?.Box);

        Assert.Equal(MonumentSide, box.SizeX);
        Assert.Equal(MonumentSide, box.SizeZ);
        Assert.Equal(monument.X - MonumentSide / 2, box.MinX);
        Assert.Equal(monument.Z - MonumentSide / 2, box.MinZ);
    }

    [Fact]
    public void DescribeStructure_RuinedPortals_ComeInSeveralVariants()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);

        var variants = generator.FindStructures(StructureType.RuinedPortal, _area)
            .Select(portal => generator.DescribeStructure(StructureType.RuinedPortal, portal).Variant)
            .ToArray();

        Assert.All(variants, variant => Assert.NotNull(variant));
        Assert.Contains(variants, variant => variant!.IsUnderground);
        Assert.Contains(variants, variant => !variant!.IsUnderground);
        Assert.All(variants, variant => Assert.InRange(variant!.Start, 1, 10));
    }

    [Fact]
    public void DescribeStructure_TypeWithoutDetails_ReturnsNoVariantAndNoPieces()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var treasure = generator.FindStructures(StructureType.Treasure, new BlockArea(0, 0, 4096, 4096))[0];

        var details = generator.DescribeStructure(StructureType.Treasure, treasure);

        Assert.Null(details.Variant);
        Assert.Empty(details.Pieces);
    }

    [Fact]
    public void DescribeStructure_Mineshaft_ReturnsCorridorsThatStartInItsChunk()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var mineshaft = generator.FindStructures(StructureType.Mineshaft, new BlockArea(0, 0, 4096, 4096))[0];

        var details = generator.DescribeStructure(StructureType.Mineshaft, mineshaft);

        Assert.Null(details.Variant);
        Assert.True(details.Pieces.Length > 1);
        Assert.Equal(mineshaft.X >> 4, details.Pieces[0].Box.MinX >> 4);
        Assert.Equal(mineshaft.Z >> 4, details.Pieces[0].Box.MinZ >> 4);
        Assert.All(details.Pieces, piece => Assert.InRange(piece.Type, 0, 3));
    }

    [Theory]
    [InlineData(McVersion.V1_21_3)]
    [InlineData(McVersion.V1_12_2)]
    public void DescribeStructure_Stronghold_ReturnsItsRoomsWithOnePortalRoom(McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld);
        var stronghold = generator.FindStrongholds()[0];

        var details = generator.DescribeStructure(StructureType.Stronghold, stronghold);

        Assert.Null(details.Variant);
        Assert.True(details.Pieces.Length > 10);
        Assert.Single(details.Pieces, piece => piece.Type == StrongholdPiece.PortalRoom);
        Assert.True(Contains(details.Pieces[0].Box, stronghold.X, stronghold.Z) || details.Pieces[0].Box.MinX >> 4 == stronghold.X >> 4);
        Assert.All(details.Pieces, piece => Assert.InRange(double.Hypot(piece.Box.MinX - stronghold.X, piece.Box.MinZ - stronghold.Z), 0, 256));
    }

    [Fact]
    public void DescribeStructure_StrongholdOutsideTheOverworld_ThrowsInvalidState()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Nether);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.DescribeStructure(StructureType.Stronghold, new BlockPos(1000, 1000)));

        Assert.Equal(NativeStatus.InvalidState, exception.Status);
    }

    [Fact]
    public void FindStructureFloor_EndCity_IsAtLeastY60()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.End);

        var floors = generator.FindStructures(StructureType.EndCity, _area).Select(city => generator.FindStructureFloor(StructureType.EndCity, city)).ToArray();

        Assert.NotEmpty(floors);
        Assert.All(floors, floor => Assert.InRange(Assert.NotNull(floor), 60, 128));
    }

    [Fact]
    public void FindStructureFloor_NetherFossils_LieBelowTheHeightTheirSearchStartsAtAndAboveTheLavaSea()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Nether);
        var fossils = generator.FindStructures(StructureType.NetherFossil, new BlockArea(-1024, -1024, 2048, 2048));

        Assert.NotEmpty(fossils);
        Assert.All(fossils, fossil =>
        {
            var start = Assert.NotNull(generator.DescribeStructure(StructureType.NetherFossil, fossil).Variant?.Height);
            Assert.InRange(Assert.NotNull(generator.FindStructureFloor(StructureType.NetherFossil, fossil)), 33, start);
        });
    }

    [Fact]
    public void FindStructureFloor_Mansion_IsTheFloorOfAMansionThatGenerates()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var mansions = generator.FindStructures(StructureType.Mansion, new BlockArea(-16384, -16384, 32768, 32768));

        var floors = mansions.Select(mansion => generator.FindStructureFloor(StructureType.Mansion, mansion)).OfType<int>().ToArray();

        Assert.NotEmpty(floors);
        Assert.All(floors, floor => Assert.InRange(floor, 61, 200));
    }

    [Fact]
    public void FindStructureFloor_TypeWithoutAKnownFloor_IsNull()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);
        var village = generator.FindStructures(StructureType.Village, _area)[0];

        Assert.Null(generator.FindStructureFloor(StructureType.Village, village));
    }

    [Theory]
    [InlineData(McVersion.V1_21_3, Dimension.Overworld, true)]
    [InlineData(McVersion.V1_14_4, Dimension.Overworld, true)]
    [InlineData(McVersion.V1_13_2, Dimension.Overworld, false)]
    [InlineData(McVersion.V1_21_3, Dimension.Nether, false)]
    [InlineData(McVersion.V1_9_4, Dimension.End, true)]
    [InlineData(McVersion.V1_8_9, Dimension.End, false)]
    public void HasExactTerrain_VersionAndDimension_TellsWhetherTheSurfaceCanBeRead(McVersion version, Dimension dimension, bool expected)
    {
        using var generator = CreateGenerator(version, dimension);

        Assert.Equal(expected, generator.HasExactTerrain);
    }

    [Theory]
    [InlineData(McVersion.V1_21_3)]
    [InlineData(McVersion.V1_16_5)]
    public void GenerateSurface_Overworld_FollowsTheApproximateHeightsClosely(McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.Overworld);
        const int Cells = 16;
        var surface = new short[Cells * 4 * Cells * 4];
        var approximate = new short[Cells * Cells];

        generator.GenerateSurface(new BlockArea(-1000, 600, Cells * 4, Cells * 4), surface);
        generator.GenerateHeights(new BiomeArea(4, -250, 150, Cells, Cells, 0), approximate);

        var differences = Enumerable.Range(0, Cells * Cells)
            .Select(cell => Math.Abs(surface[(cell / Cells * 4 + 2) * Cells * 4 + cell % Cells * 4 + 2] - approximate[cell] / (double)Constants.HeightUnit))
            .ToArray();
        Assert.True(differences.Average() < 6, $"The exact surface differs from the estimate by {differences.Average():0.0} blocks on average.");
        Assert.All(surface, y => Assert.InRange(y, -64, 320));
    }

    [Fact]
    public void GenerateSurface_End_RaisesTheMainIslandOutOfTheVoid()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.End);
        var island = new short[64];
        var voidRow = new short[64];

        generator.GenerateSurface(new BlockArea(0, 0, 64, 1), island);
        generator.GenerateSurface(new BlockArea(480, 0, 64, 1), voidRow);

        Assert.True(island[0] > 40, $"The main island lies at {island[0]}.");
        Assert.Contains((short)0, voidRow);
    }

    [Fact]
    public void GenerateSurface_VersionWithoutExactTerrain_IsRejected()
    {
        using var generator = CreateGenerator(McVersion.V1_13_2, Dimension.Overworld);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.GenerateSurface(new BlockArea(0, 0, 4, 4), new short[16]));

        Assert.Equal(NativeStatus.Unsupported, exception.Status);
    }

    [Fact]
    public void DescribeStructure_OtherDimensionApplied_ThrowsInvalidState()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.DescribeStructure(StructureType.EndCity, new BlockPos(2000, 2000)));

        Assert.Equal(NativeStatus.InvalidState, exception.Status);
    }

    [Theory]
    [InlineData(McVersion.V1_21_3)]
    [InlineData(McVersion.V1_16_5)]
    [InlineData(McVersion.V1_13_2)]
    public void FindEndGateways_FollowableVersion_ReturnsTheRingOfGatewaysAndTheirDestinations(McVersion version)
    {
        using var generator = CreateGenerator(version, Dimension.End);

        var gateways = generator.FindEndGateways();

        Assert.Equal(20, gateways.Length);
        Assert.Equal(20, gateways.Select(gateway => gateway.Position).Distinct().Count());
        Assert.All(gateways, gateway =>
        {
            Assert.InRange(double.Hypot(gateway.Position.X, gateway.Position.Z), GatewayRingBlocks - 2, GatewayRingBlocks + 2);
            Assert.InRange(double.Hypot(gateway.Destination.X, gateway.Destination.Z), 700, 1400);
        });
        Assert.Equal(gateways, generator.FindEndGateways());
    }

    [Fact]
    public void FindEndGateways_VersionBefore113_ReturnsNothing()
    {
        using var generator = CreateGenerator(McVersion.V1_12_2, Dimension.End);

        Assert.Empty(generator.FindEndGateways());
    }

    [Fact]
    public void FindEndGateways_OverworldApplied_ThrowsInvalidState()
    {
        using var generator = CreateGenerator(McVersion.V1_21_3, Dimension.Overworld);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.FindEndGateways());

        Assert.Equal(NativeStatus.InvalidState, exception.Status);
    }

    private static CubiomesWorldGenerator CreateGenerator(McVersion version, Dimension dimension)
    {
        var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, Seed);
        return generator;
    }

    private static bool Contains(BlockBox box, int x, int z) => x >= box.MinX && x <= box.MaxX && z >= box.MinZ && z <= box.MaxZ;
}
