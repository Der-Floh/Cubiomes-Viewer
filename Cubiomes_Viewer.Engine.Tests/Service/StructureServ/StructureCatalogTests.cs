using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

public sealed class StructureCatalogTests
{
    private readonly StructureCatalog _catalog = new(new CubiomesStructureFinder());

    [Fact]
    public void Structures_Listed_AreUniqueNamedAndLocatable()
    {
        var types = _catalog.Structures.Select(structure => structure.Type).ToArray();

        Assert.Equal(types.Length, types.Distinct().Count());
        Assert.DoesNotContain(StructureType.Feature, types);
        Assert.DoesNotContain(StructureType.EndIsland, types);
        Assert.All(_catalog.Structures, structure =>
        {
            Assert.NotEmpty(structure.Name);
            Assert.True(structure.MaxBlocksPerPixel > 0);
            Assert.Same(structure, _catalog.GetInfo(structure.Type));
        });
    }

    [Fact]
    public void GetAvailable_NewestOverworld_ListsOverworldStructuresOnly()
    {
        var types = _catalog.GetAvailable(McVersion.V1_21_3, Dimension.Overworld).Select(structure => structure.Type).ToArray();

        Assert.Contains(StructureType.Village, types);
        Assert.Contains(StructureType.TrialChambers, types);
        Assert.Contains(StructureType.Mineshaft, types);
        Assert.DoesNotContain(StructureType.Fortress, types);
        Assert.DoesNotContain(StructureType.EndCity, types);
    }

    [Fact]
    public void GetAvailable_NetherAndEnd_ListTheirOwnStructures()
    {
        var nether = _catalog.GetAvailable(McVersion.V1_21_3, Dimension.Nether).Select(structure => structure.Type);
        var end = _catalog.GetAvailable(McVersion.V1_21_3, Dimension.End).Select(structure => structure.Type);

        Assert.Equal([StructureType.Fortress, StructureType.Bastion, StructureType.RuinedPortalNether], nether);
        Assert.Equal([StructureType.EndCity, StructureType.EndGateway], end);
    }

    [Fact]
    public void GetAvailable_OlderVersion_LeavesOutLaterStructures()
    {
        var types = _catalog.GetAvailable(McVersion.V1_12_2, Dimension.Overworld).Select(structure => structure.Type).ToArray();

        Assert.Contains(StructureType.Mansion, types);
        Assert.DoesNotContain(StructureType.Shipwreck, types);
        Assert.DoesNotContain(StructureType.AncientCity, types);
        Assert.Empty(_catalog.GetAvailable(McVersion.Beta1_7, Dimension.Overworld));
    }

    [Fact]
    public void GetInfo_TypeTheMapCannotShow_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _catalog.GetInfo(StructureType.Feature));
}
