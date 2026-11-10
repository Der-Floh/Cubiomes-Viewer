using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.DependencyInjection;

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

        // world generation
        services.AddSingleton<IWorldGeneratorFactory, CubiomesWorldGeneratorFactory>();
        services.AddSingleton<IVersionCatalog, VersionCatalog>();
        services.AddSingleton<IBiomeCatalog, BiomeCatalog>();
        services.AddSingleton<IStructureFinder, CubiomesStructureFinder>();

        // compute
        services.AddSingleton<JobHandlerRegistry>();
        services.AddJobHandler<BiomeTileJob, TileData, BiomeTileJobHandler>();
        if (OperatingSystem.IsBrowser())
            services.AddSingleton<IComputeService, InlineComputeService>();
        else
            services.AddSingleton<IComputeService, InProcessComputeService>();

        // services
        services.AddSingleton<ITileColorizer, BiomeTileColorizer>();
        services.AddSingleton<IMapTileService, MapTileService>();
        services.AddSingleton<ISeedParser, SeedParser>();
        services.AddSingleton<ISeedHistoryService, SeedHistoryService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISelfTestService, SelfTestService>();

        return services;
    }

    private static IServiceCollection AddJobHandler<TJob, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where TJob : ComputeJob<TResult>
        where THandler : class, IJobHandler<TJob, TResult>
    {
        services.AddSingleton<THandler>();
        services.AddSingleton(provider => JobHandlerRegistration.Create(provider.GetRequiredService<THandler>()));
        return services;
    }
}
