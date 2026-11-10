using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Service.NotificationServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests;

/// <summary>Builds the application's services with storage, compute and notifications replaced by test-safe ones.</summary>
internal static class TestServices
{
    public static ServiceProvider Create()
    {
        var services = new ServiceCollection();
        App.ConfigureServices(services);
        UseIsolatedPlatform(services);
        return services.BuildServiceProvider();
    }

    public static void UseIsolatedPlatform(IServiceCollection services)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
        services.AddSingleton<IAppDataStore>(new FileSystemAppDataStore(directory));
        services.AddSingleton<IComputeService, InlineComputeService>();
        services.AddSingleton<INotificationService, RecordingNotificationService>();
    }
}
