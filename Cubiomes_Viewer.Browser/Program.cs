using System.Runtime.InteropServices.JavaScript;

using Avalonia;
using Avalonia.Browser;
using Avalonia.Logging;

using Cubiomes_Viewer;
using Cubiomes_Viewer.Browser.Service;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Service.AppAddressServ;
using Cubiomes_Viewer.Service.MapLinkServ;
using Cubiomes_Viewer.Service.ViewHistoryServ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal sealed partial class Program
{
    /// <param name="args">The page's address, which main.js hands over.</param>
    private static Task Main(string[] args)
    {
        var startAddress = args.FirstOrDefault();
        App.ConfigurePlatformServices = services => ConfigureBrowserServices(services, startAddress);

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

    private static void ConfigureBrowserServices(IServiceCollection services, string? startAddress)
    {
        services.AddSingleton<IAppDataStore, BrowserAppDataStore>();
        services.AddSingleton<IComputeWorkerFactory, WebWorkerFactory>();
        services.AddSingleton<IComputeService, WorkerPoolComputeService>();
        services.AddSingleton(provider => new BrowserAppAddressService(startAddress, provider.GetRequiredService<IMapLinkService>(), provider.GetRequiredService<ILogger<BrowserAppAddressService>>()));
        services.AddSingleton<IAppAddressService>(provider => provider.GetRequiredService<BrowserAppAddressService>());
        services.AddSingleton<IViewHistoryService>(provider => provider.GetRequiredService<BrowserAppAddressService>());
    }
}
