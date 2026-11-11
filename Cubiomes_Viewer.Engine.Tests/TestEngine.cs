using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests;

/// <summary>Builds engine services with their real collaborators for tests that need no container.</summary>
internal static class TestEngine
{
    public static StructureCatalog CreateStructureCatalog() => new(new CubiomesStructureFinder());

    public static LayerCatalog CreateLayerCatalog() => new(CreateStructureCatalog());

    public static TileColorizer CreateTileColorizer() => new(new BiomeCatalog(), CreateLayerCatalog(), new CubiomesStructureFinder());
}
