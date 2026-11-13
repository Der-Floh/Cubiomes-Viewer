using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

/// <summary>Every structure has exactly one option of each feature of its type, and it is the one its details describe.</summary>
public sealed class StructureFeatureCatalogTests
{
    private const long Seed = 262;
    private const McVersion Version = McVersion.V1_21_3;
    private const int Side = 8000;
    private const int GeodeSide = 512;

    private readonly StructureFeatureCatalog _catalog = new();

    public static TheoryData<StructureType, McVersion, Dimension, int> TypesWithFeatures() => new()
    {
        { StructureType.Village, Version, Dimension.Overworld, Side },
        { StructureType.Village, McVersion.V1_12_2, Dimension.Overworld, Side },
        { StructureType.Bastion, Version, Dimension.Nether, Side },
        { StructureType.Fortress, Version, Dimension.Nether, Side },
        { StructureType.RuinedPortal, Version, Dimension.Overworld, Side },
        { StructureType.RuinedPortalNether, Version, Dimension.Nether, Side },
        { StructureType.Igloo, Version, Dimension.Overworld, Side },
        { StructureType.Geode, Version, Dimension.Overworld, GeodeSide },
        { StructureType.EndCity, Version, Dimension.End, Side },
    };

    [Theory]
    [MemberData(nameof(TypesWithFeatures))]
    public void GetOptions_EveryStructure_HasExactlyOneOptionOfEachFeature(StructureType type, McVersion version, Dimension dimension, int side)
    {
        var features = _catalog.GetFeatures(type, version);
        var structures = Describe(type, version, dimension, side);

        Assert.NotEmpty(features);
        Assert.NotEmpty(structures);
        Assert.All(structures, details =>
        {
            var options = _catalog.GetOptions(details, version);
            Assert.All(features, feature => Assert.Single(feature.Options, option => (options & option.Bit) != 0));
        });
    }

    [Fact]
    public void GetOptions_Fortresses_SayWhetherTheyHaveANetherWartRoomAsTheirPiecesDo()
    {
        var with = _catalog.GetFeatures(StructureType.Fortress, Version).Single(feature => feature.Key == "netherWartRoom").Options.Single(option => option.Key == "with");
        var structures = Describe(StructureType.Fortress, Version, Dimension.Nether, Side);

        Assert.Contains(structures, HasNetherWartRoom);
        Assert.Contains(structures, details => !HasNetherWartRoom(details));
        Assert.All(structures, details => Assert.Equal(HasNetherWartRoom(details), (_catalog.GetOptions(details, Version) & with.Bit) != 0));
    }

    [Fact]
    public void GetOptions_Bastions_HaveTheOptionOfTheirType()
    {
        var type = _catalog.GetFeatures(StructureType.Bastion, Version).Single();
        var structures = Describe(StructureType.Bastion, Version, Dimension.Nether, Side);

        Assert.Equal(type.Options.Count, structures.Select(details => details.Variant?.Start).Distinct().Count());
        Assert.All(structures, details => Assert.Equal(type.Options[details.Variant!.Start].Bit, _catalog.GetOptions(details, Version)));
    }

    [Fact]
    public void GetFeatures_InEachVersion_AreTheOnesTheGeneratorCanTellApart()
    {
        Assert.Empty(_catalog.GetFeatures(StructureType.Village, McVersion.V1_9_4));
        Assert.Equal(["villagers"], GetKeys(StructureType.Village, McVersion.V1_12_2));
        Assert.Equal(["type", "villagers"], GetKeys(StructureType.Village, Version));
        Assert.Equal(["size", "placement"], GetKeys(StructureType.RuinedPortal, Version));
        Assert.Equal(["size"], GetKeys(StructureType.RuinedPortalNether, Version));
        Assert.Equal(["netherWartRoom", "blazeSpawner"], GetKeys(StructureType.Fortress, Version));
        Assert.Empty(_catalog.GetFeatures(StructureType.DesertPyramid, Version));
    }

    [Fact]
    public void GetFeatures_OfEveryType_GiveEachOptionABitOfItsOwn()
    {
        foreach (var type in Enum.GetValues<StructureType>())
        {
            ulong[] bits = [.. _catalog.GetFeatures(type, Version).SelectMany(feature => feature.Options).Select(option => option.Bit)];

            Assert.All(bits, bit => Assert.True(ulong.IsPow2(bit)));
            Assert.Equal(bits.Length, bits.Distinct().Count());
        }
    }

    private string[] GetKeys(StructureType type, McVersion version) => [.. _catalog.GetFeatures(type, version).Select(feature => feature.Key)];

    private static bool HasNetherWartRoom(StructureDetails details) => details.Pieces.Any(piece => piece.Type == FortressPiece.NetherWartRoom);

    private static List<StructureDetails> Describe(StructureType type, McVersion version, Dimension dimension, int side)
    {
        using var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, Seed);
        var area = new BlockArea(-side / 2, -side / 2, side, side);
        return [.. generator.FindStructures(type, area).Select(position => generator.DescribeStructure(type, position))];
    }
}
