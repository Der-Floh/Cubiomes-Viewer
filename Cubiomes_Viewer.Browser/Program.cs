using Avalonia;
using Avalonia.Browser;
using Avalonia.Logging;

using Cubiomes_Viewer;
using Cubiomes_Viewer.Browser.Service;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

using Microsoft.Extensions.DependencyInjection;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App.ConfigurePlatformServices = ConfigureBrowserServices;

        return BuildAvaloniaApp()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
            .LogToTextWriter(Console.Out, LogEventLevel.Warning)
#endif
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();

    private static void ConfigureBrowserServices(IServiceCollection services)
    {
        services.AddSingleton<IAppDataStore, BrowserAppDataStore>();
        services.AddSingleton<IComputeWorkerFactory, WebWorkerFactory>();
        services.AddSingleton<IComputeService, WorkerPoolComputeService>();
    }
}
