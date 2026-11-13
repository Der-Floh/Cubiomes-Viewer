using System.Runtime.InteropServices.JavaScript;

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

    /// <summary>Called by the page when it is hidden, as a closed tab never runs the delayed saves.</summary>
    [JSExport]
    internal static Task FlushStoredStateAsync() => App.FlushStoredStateAsync();

    private static void ConfigureBrowserServices(IServiceCollection services)
    {
        services.AddSingleton<IAppDataStore, BrowserAppDataStore>();
        services.AddSingleton<IComputeWorkerFactory, WebWorkerFactory>();
        services.AddSingleton<IComputeService, WorkerPoolComputeService>();
    }
}
