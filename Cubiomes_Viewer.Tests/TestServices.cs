using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Service.AppAddressServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests;

/// <summary>Builds the application's services with storage, compute, notifications, the clipboard, the file pickers and the address bar replaced by test-safe ones.</summary>
internal static class TestServices
{
    /// <param name="configure">Replaces further services, such as one a test fakes.</param>
    public static ServiceProvider Create(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        App.ConfigureServices(services);
        UseIsolatedPlatform(services);
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public static void UseIsolatedPlatform(IServiceCollection services)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
        services.AddSingleton<IAppDataStore>(new FileSystemAppDataStore(directory));
        services.AddSingleton<IComputeService, InlineComputeService>();
        services.AddSingleton<INotificationService, RecordingNotificationService>();
        services.AddSingleton<IClipboardService, RecordingClipboardService>();
        services.AddSingleton<IStorageService, MemoryStorageService>();
        services.AddSingleton<IAppAddressService, RecordingAppAddressService>();
    }
}
