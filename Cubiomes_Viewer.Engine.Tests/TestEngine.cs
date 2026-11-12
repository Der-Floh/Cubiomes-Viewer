using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
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

    public static ConditionRegistry CreateConditionRegistry() => new(
    [
        new StructureConditionEvaluator(new CubiomesStructureFinder(), CreateStructureCatalog()),
        new QuadHutConditionEvaluator(new CubiomesQuadFinder(), new CubiomesStructureFinder(), CreateStructureCatalog()),
        new BiomeConditionEvaluator(new BiomeCatalog()),
        new BiomePatchConditionEvaluator(new BiomeCatalog()),
        new ClimateConditionEvaluator(CreateLayerCatalog()),
        new SpawnConditionEvaluator(),
        new StrongholdConditionEvaluator(),
        new SlimeChunkConditionEvaluator(new CubiomesSlimeChunkFinder()),
        new AnyOfConditionEvaluator(),
    ]);

    public static ConditionValidator CreateConditionValidator() => new(CreateConditionRegistry(), new VersionCatalog());

    public static SearchPlanCompiler CreateSearchPlanCompiler() => new(CreateConditionRegistry());

    /// <summary>A factory for sources other than quad huts, whose bases these tests never need.</summary>
    public static SeedSequenceFactory CreateSeedSequenceFactory() => new(new NoQuadHutBases(), new CubiomesQuadFinder(), CreateStructureCatalog());

    private sealed class NoQuadHutBases : IQuadHutBaseService
    {
        public Task<IReadOnlyList<ulong>> GetBasesAsync(QuadQuality worst, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("These tests search no quad-hut seeds.");
    }
}
