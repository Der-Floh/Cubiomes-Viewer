using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.StructureDescriptionServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class StructureDescriptionServiceTests : IDisposable
{
    private const McVersion Version = McVersion.V1_21_3;
    private const int PlainsBiomeId = 1;
    private static readonly BlockPos _position = new(100, -200);
    private static readonly BlockBox _box = new(96, 60, -208, 105, 71, -200);

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly IStructureDescriptionService _service;

    public StructureDescriptionServiceTests()
    {
        _service = _services.GetRequiredService<IStructureDescriptionService>();
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public void Describe_AbandonedVillage_NamesItsBiomeAndItsStartPiece()
    {
        var variant = new StructureVariant { Biome = PlainsBiomeId, IsAbandoned = true, Box = _box };

        var lines = _service.Describe(Create(StructureType.Village, variant), Version);

        Assert.Equal(["Plains village", "Abandoned (zombie village)", "Start piece: 10 × 12 × 9 blocks"], lines);
    }

    [Fact]
    public void Describe_Bastion_NamesItsKind()
    {
        var lines = _service.Describe(Create(StructureType.Bastion, new StructureVariant { Start = 2 }), Version);

        Assert.Equal(["Treasure room"], lines);
    }

    [Fact]
    public void Describe_RuinedPortals_TellWhereTheyStand()
    {
        var buried = _service.Describe(Create(StructureType.RuinedPortal, new StructureVariant { IsGiant = true, IsUnderground = true, HasAirPocket = true }), Version);
        var inAir = _service.Describe(Create(StructureType.RuinedPortalNether, new StructureVariant { HasAirPocket = true }), Version);
        var plain = _service.Describe(Create(StructureType.RuinedPortal, new StructureVariant()), Version);

        Assert.Equal(["Giant portal", "Buried underground"], buried);
        Assert.Equal(["In an air pocket"], inAir);
        Assert.Empty(plain);
    }

    [Fact]
    public void Describe_IglooAndGeode_TellTheirVariantHeightAndSize()
    {
        var igloo = _service.Describe(Create(StructureType.Igloo, new StructureVariant { HasBasement = true, Box = _box }), Version);
        var geode = _service.Describe(Create(StructureType.Geode, new StructureVariant { IsCracked = true, Height = -20 }), Version);

        Assert.Equal(["Has a basement", "Size: 10 × 12 × 9 blocks"], igloo);
        Assert.Equal(["Cracked open", "Around Y -20"], geode);
    }

    [Fact]
    public void Describe_MonumentWithUnknownHeight_TellsOnlyItsFootprint()
    {
        var footprint = new BlockBox(-29, 0, -29, 28, 0, 28);

        var lines = _service.Describe(Create(StructureType.Monument, new StructureVariant { Box = footprint }), Version);

        Assert.Equal(["Size: 58 × 58 blocks"], lines);
    }

    [Fact]
    public void Describe_EndCity_CountsItsPiecesAndLooksForAShip()
    {
        StructurePiece[] withShip = [new(0, 0, 0, _box), new(EndCityPiece.Ship, 0, 3, _box)];
        StructurePiece[] withoutShip = [new(0, 0, 0, _box)];

        Assert.Equal(["2 pieces", "Has an end ship"], _service.Describe(Create(StructureType.EndCity, null, withShip), Version));
        Assert.Equal(["1 piece", "No end ship"], _service.Describe(Create(StructureType.EndCity, null, withoutShip), Version));
    }

    [Fact]
    public void Describe_Fortress_CountsSpawnersAndNetherWartRooms()
    {
        StructurePiece[] pieces =
        [
            new(0, 0, 0, _box), new(FortressPiece.BlazeSpawner, 0, 1, _box), new(FortressPiece.NetherWartRoom, 0, 2, _box),
            new(FortressPiece.NetherWartRoom, 0, 3, _box),
        ];

        var lines = _service.Describe(Create(StructureType.Fortress, null, pieces), Version);

        Assert.Equal(["4 pieces", "1 blaze spawner", "2 nether wart rooms"], lines);
    }

    [Fact]
    public void Describe_StructureWithoutDetails_SaysNothing()
    {
        Assert.Empty(_service.Describe(Create(StructureType.Mansion, null), Version));
    }

    private static StructureDetails Create(StructureType type, StructureVariant? variant, StructurePiece[]? pieces = null)
        => new() { Type = type, Position = _position, Variant = variant, Pieces = pieces ?? [] };
}
