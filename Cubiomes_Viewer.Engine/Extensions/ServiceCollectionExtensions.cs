using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SessionRepo;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cubiomes_Viewer.Engine.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the engine services. Hosts must also register logging (<c>AddLogging</c>) and may replace
    /// <see cref="IComputeService"/> or <see cref="IAppDataStore"/> with platform implementations afterwards.
    /// </summary>
    public static IServiceCollection AddCubiomesEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // repositories
        services.AddSingleton<IAppDataStore, FileSystemAppDataStore>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<ISearchSessionRepository, SearchSessionRepository>();

        // world generation
        services.AddSingleton<IWorldGeneratorFactory, CubiomesWorldGeneratorFactory>();
        services.AddSingleton<IVersionCatalog, VersionCatalog>();
        services.AddSingleton<IBiomeCatalog, BiomeCatalog>();
        services.AddSingleton<ILayerCatalog, LayerCatalog>();
        services.AddSingleton<IStructureFinder, CubiomesStructureFinder>();
        services.AddSingleton<ISlimeChunkFinder, CubiomesSlimeChunkFinder>();
        services.AddSingleton<IQuadFinder, CubiomesQuadFinder>();

        // compute
        services.AddSingleton<JobHandlerRegistry>();
        services.AddSingleton<ComputeWorkerHost>();
        services.AddJobHandler<TileJob, TileData, TileJobHandler>();
        services.AddJobHandler<StructureAreaJob, StructureAreaData, StructureAreaJobHandler>();
        services.AddJobHandler<StructureDetailsJob, StructureDetails, StructureDetailsJobHandler>();
        services.AddJobHandler<LandmarksJob, WorldLandmarks, LandmarksJobHandler>();
        services.AddJobHandler<EndGatewaysJob, EndGatewayData, EndGatewaysJobHandler>();
        services.AddJobHandler<SlimeAreaJob, SlimeAreaData, SlimeAreaJobHandler>();
        services.AddJobHandler<SelfTestJob, SelfTestReport, SelfTestJobHandler>();
        services.AddJobHandler<SearchBatchJob, SearchBatchResult, SearchBatchJobHandler>();
        services.AddJobHandler<QuadHutBaseScanJob, QuadHutBaseScan, QuadHutBaseScanJobHandler>();
        if (OperatingSystem.IsBrowser())
            services.AddSingleton<IComputeService, InlineComputeService>();
        else
            services.AddSingleton<IComputeService, InProcessComputeService>();

        // seed search
        services.AddSingleton<IConditionEvaluator, StructureConditionEvaluator>();
        services.AddSingleton<IConditionEvaluator, QuadHutConditionEvaluator>();
        services.AddSingleton<IConditionEvaluator, BiomeConditionEvaluator>();
        services.AddSingleton<IConditionEvaluator, SpawnConditionEvaluator>();
        services.AddSingleton<IConditionEvaluator, StrongholdConditionEvaluator>();
        services.AddSingleton<IConditionEvaluator, SlimeChunkConditionEvaluator>();
        services.AddSingleton<IConditionEvaluator, AnyOfConditionEvaluator>();
        services.AddSingleton<IConditionRegistry, ConditionRegistry>();
        services.AddSingleton<IConditionValidator, ConditionValidator>();
        services.AddSingleton<ISearchPlanCompiler, SearchPlanCompiler>();
        services.AddSingleton<ISeedMatcher, SeedMatcher>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IQuadHutBaseService, QuadHutBaseService>();
        services.AddSingleton<ISeedSequenceFactory, SeedSequenceFactory>();
        services.AddSingleton<ISeedSearchService, SeedSearchService>();
        services.AddSingleton<ISearchSessionService, SearchSessionService>();

        // services
        services.AddSingleton<ITileColorizer, TileColorizer>();
        services.AddSingleton<IMapTileService, MapTileService>();
        services.AddSingleton<IStructureCatalog, StructureCatalog>();
        services.AddSingleton<IStructureAreaService, StructureAreaService>();
        services.AddSingleton<IWorldLandmarkService, WorldLandmarkService>();
        services.AddSingleton<IEndGatewayService, EndGatewayService>();
        services.AddSingleton<ISlimeAreaService, SlimeAreaService>();
        services.AddSingleton<ISeedParser, SeedParser>();
        services.AddSingleton<ISeedHistoryService, SeedHistoryService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISelfTestService, SelfTestService>();
        services.AddSingleton<ITileBenchmarkService, TileBenchmarkService>();

        return services;
    }

    private static IServiceCollection AddJobHandler<TJob, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where TJob : ComputeJob<TResult>
        where TResult : notnull
        where THandler : class, IJobHandler<TJob, TResult>
    {
        services.AddSingleton<THandler>();
        services.AddSingleton(provider => JobHandlerRegistration.Create(provider.GetRequiredService<THandler>()));
        return services;
    }
}
